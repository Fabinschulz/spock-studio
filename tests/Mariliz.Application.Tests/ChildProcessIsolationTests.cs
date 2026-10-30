using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// Achado no aplicativo instalado: um filho sem entrada própria herdava a entrada do motor, que ali é o canal do protocolo
/// com o host. Um script que lê a entrada ficava esperando o limite de tempo e podia engolir mensagens do protocolo.
/// </summary>
public class ChildProcessIsolationTests
{
    [Fact]
    public void Todo_processo_filho_tem_a_entrada_redirecionada_mesmo_sem_texto_para_enviar()
    {
        var withoutInput = SystemProcessRunner.BuildStartInfo(new ProcessSpec("python", ["x.py"], ".", TimeSpan.FromSeconds(5)));
        var withInput = SystemProcessRunner.BuildStartInfo(new ProcessSpec("python", ["x.py"], ".", TimeSpan.FromSeconds(5), "{}"));

        Assert.True(withoutInput.RedirectStandardInput);
        Assert.True(withInput.RedirectStandardInput);
        Assert.False(withoutInput.UseShellExecute);
    }

    [Fact]
    public async Task Filho_que_le_a_entrada_recebe_fim_de_arquivo_na_hora_em_vez_de_esperar_o_limite()
    {
        var python = OperatingSystem.IsWindows() ? "python" : "python3";
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var result = await new SystemProcessRunner().RunAsync(
            new ProcessSpec(python, ["-c", "import sys; print(len(sys.stdin.read()))"], Path.GetTempPath(), TimeSpan.FromSeconds(15)), CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("0", result.StandardOutput.Trim());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"levou {sw.Elapsed.TotalSeconds:0.0}s");
    }

    [Fact]
    public async Task Script_de_validacao_que_estoura_o_tempo_deixa_o_eval_bloqueado_e_nao_reprovado()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var journey = new Journey(project, (spec, _) => Task.FromResult<ProcessResult?>(
            spec.Arguments.Any(a => a.EndsWith("check_commit.py", StringComparison.Ordinal)) ? new ProcessResult(-1, "", "", true, false) : null));

        var result = await journey.ApplyAsync(await journey.PlanAsync());

        Assert.Equal("blocked", Journey.Status(result, "EV-SPEC-001"));
        Assert.NotEqual("failed", result["outcome"]!.GetValue<string>());
        var evidence = result["validation"]!["results"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "EV-SPEC-001")!["evidence"]!.AsArray();
        Assert.Contains(evidence, e => e!.GetValue<string>().Contains("excedeu"));
    }
}
