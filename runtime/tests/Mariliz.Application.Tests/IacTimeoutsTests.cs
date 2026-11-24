using System.Text.RegularExpressions;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// NET-S08: o motor responde antes de o host desistir. Lê a espera de cada método em engine/timeouts.rs e confere com o total da
// operação no motor, mais a folga do serviço.
public sealed partial class IacTimeoutsTests
{
    private static readonly IacInstallation Installation =
        new(IacEngine.OpenTofu, IacRuntimeSource.Managed, "1.13.9", "tofu", true, true, false, null, null);

    [Fact]
    public void Cada_operacao_do_motor_termina_antes_da_espera_do_host()
    {
        var host = HostWaits();

        foreach (var (method, total) in IacTimeouts.ByMethod)
        {
            Assert.True(host.ContainsKey(method), $"{method} não tem espera própria no host: cairia nos 120 s padrão.");
            Assert.True(total + IacTimeouts.HostMargin <= host[method],
                $"{method}: o motor pode levar {total.TotalMinutes} min mais {IacTimeouts.HostMargin.TotalMinutes} de folga, e o host espera {host[method].TotalMinutes}.");
        }
    }

    [Fact]
    public void Todo_metodo_de_IaC_com_espera_longa_no_host_tem_total_no_motor()
    {
        var longWaits = HostWaits().Where(wait => wait.Key.StartsWith("iac.", StringComparison.Ordinal)
            && wait.Key is not (ProtocolMethods.IacRuntimeInstall or ProtocolMethods.IacArchitectDraft)).Select(wait => wait.Key);

        Assert.Equal(longWaits.Order(StringComparer.Ordinal), IacTimeouts.ByMethod.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void O_total_do_apply_nunca_corta_o_apply_em_andamento()
    {
        Assert.True(IacTimeouts.Init + IacTimeouts.Short + IacTimeouts.Apply <= IacTimeouts.ApplyTotal);
    }

    [Fact]
    public void Cada_etapa_ganha_o_proprio_prazo_ou_o_que_sobrou_do_total()
    {
        var time = new ManualTime();
        var deadline = new IacDeadline(TimeSpan.FromMinutes(40), time);

        Assert.Equal(IacTimeouts.Plan, deadline.Stage(IacTimeouts.Plan));
        time.Advance(TimeSpan.FromMinutes(25));
        Assert.Equal(TimeSpan.FromMinutes(15), deadline.Stage(IacTimeouts.Plan));
        time.Advance(TimeSpan.FromMinutes(20));
        Assert.Equal(TimeSpan.Zero, deadline.Stage(IacTimeouts.Short));
        Assert.Equal(IacTimeouts.Apply, IacDeadline.Unlimited.Stage(IacTimeouts.Apply));
    }

    [Fact]
    public async Task Apply_demorado_deixa_para_a_verificacao_so_o_que_sobrou_e_sem_tempo_a_etapa_nem_comeca()
    {
        var time = new ManualTime();
        var calls = new List<(string Command, TimeSpan Timeout)>();
        var runner = new InterceptingRunner((spec, _) =>
        {
            calls.Add((spec.Arguments[0], spec.Timeout));
            time.Advance(spec.Arguments[0] switch { "apply" => TimeSpan.FromMinutes(60), "plan" => spec.Timeout, _ => TimeSpan.Zero });
            return Task.FromResult<ProcessResult?>(spec.Arguments[0] == "plan"
                ? new ProcessResult(-1, "", "", TimedOut: true, NotFound: false)
                : new ProcessResult(0, "", "", false, false));
        });
        var directory = Directory.CreateTempSubdirectory("spock-prazos-").FullName;
        try
        {
            var runtime = new CliIacRuntime(Installation, runner, deadline: new IacDeadline(IacTimeouts.ApplyTotal, time));

            var run = await runtime.ApplyAsync(new IacApplySpec(directory, Path.Combine(directory, "data"), Path.Combine(directory, "plan.tfplan"), null,
                new Dictionary<string, string>(), Path.Combine(directory, "apply.log")), CancellationToken.None);

            Assert.True(run.Applied);
            Assert.False(run.Verified);
            Assert.Equal(IacTimeouts.Apply, calls.Single(call => call.Command == "apply").Timeout);
            // 85 min de total e 60 no apply: a verificação ganha os 25 que sobraram, não os 30 dela.
            Assert.Equal(TimeSpan.FromMinutes(25), calls.Single(call => call.Command == "plan").Timeout);
            Assert.Equal(["init", "apply", "plan"], calls.Select(call => call.Command));
            Assert.Contains("verificação depois do apply", run.Error);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // Lê as constantes e o match de request_timeout; só o formato que engine/timeouts.rs usa hoje.
    private static Dictionary<string, TimeSpan> HostWaits()
    {
        var rust = File.ReadAllText(Path.Combine(ContractFiles.Root, "desktop", "host", "src-tauri", "src", "engine", "timeouts.rs"));
        var methods = MethodConst().Matches(rust).ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var durations = DurationConst().Matches(rust).ToDictionary(m => m.Groups[1].Value, m => TimeSpan.FromSeconds(int.Parse(m.Groups[2].Value)));
        var body = MatchBody().Match(rust);
        Assert.True(body.Success, "engine/timeouts.rs não tem mais o match de request_timeout no formato esperado.");

        var waits = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        foreach (var arm in body.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = arm.Split("=>", StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0] == "_") continue;
            foreach (var name in parts[0].Split('|', StringSplitOptions.TrimEntries))
                waits[methods[name]] = durations[parts[1]];
        }
        return waits;
    }

    [GeneratedRegex("""const (\w+_METHOD): &str = "([^"]+)";""")]
    private static partial Regex MethodConst();

    [GeneratedRegex(@"const (\w+_TIMEOUT): Duration = Duration::from_secs\((\d+)\);")]
    private static partial Regex DurationConst();

    [GeneratedRegex(@"fn request_timeout\(method: &str\) -> Duration \{\s*match method \{(.*?)\n\s*\}\s*\n\}", RegexOptions.Singleline)]
    private static partial Regex MatchBody();

    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
