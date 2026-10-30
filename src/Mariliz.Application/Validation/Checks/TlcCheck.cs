using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class TlcCheck : IEvalCheck
{
    public string Id => EvalIds.Tlc;

    public async Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct)
    {
        var tlc = TlcInspector.Inspect(ctx.Files);
        var evidence = new List<string>();
        var limitations = new List<string>();

        if (tlc.State == TlcState.NotInstalled) return Block(spec, "O TLC não está instalado neste projeto.");
        if (tlc.State is TlcState.Invalid or TlcState.Drifted)
            return Fail(spec, tlc.State == TlcState.Invalid ? "A instalação do TLC está incompleta ou inválida." : "A instalação do TLC difere do hash do instalador.", tlc.Problems);

        evidence.Add($"{TlcManifest.SkillDirectory}: {TlcManifest.RequiredFiles.Count} arquivos exigidos presentes");
        evidence.Add($"Hash conferido com {TlcManifest.LockFile} ({tlc.LockedHash![..12]}…)");
        if (!tlc.VersionTested) limitations.Add($"A versão {tlc.Version ?? "desconhecida"} do TLC não foi validada por esta fase (validada: {string.Join(", ", TlcManifest.TestedVersions)}).");
        else evidence.Add($"Versão {tlc.Version} validada por esta fase");

        var python = ctx.Discovery.Tools.PythonCommand;
        if (python is null) return Block(spec, "Python 3 não está disponível; os gates do TLC não puderam ser executados.");

        var scripts = Path.Combine(ctx.RootPath, TlcManifest.SkillDirectory.Replace('/', Path.DirectorySeparatorChar), "scripts");
        var good = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "feat(core): add validation"], ctx.RootPath, EvalTimeouts.Script), ct);
        var bad = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "Mensagem sem tipo"], ctx.RootPath, EvalTimeouts.Script), ct);
        var self = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "lessons.py"), "selftest"], ctx.RootPath, EvalTimeouts.Script), ct);

        // Não rodar ou estourar o tempo não é uma resposta errada: é uma verificação que não pôde ser concluída.
        var notRun = new[] { ("check_commit.py (mensagem válida)", good), ("check_commit.py (mensagem inválida)", bad), ("lessons.py selftest", self) }
            .Where(x => x.Item2.TimedOut || x.Item2.NotFound)
            .Select(x => $"{x.Item1}: {(x.Item2.TimedOut ? $"excedeu {EvalTimeouts.Script.TotalSeconds:0} s" : "não pôde ser executado")}.{ProcessOutput.Tail(x.Item2)}")
            .ToList();
        if (notRun.Count > 0) return Make(spec, EvalStatus.Blocked, notRun, "Os scripts do TLC não puderam ser executados até o fim; os gates não foram verificados.", []);

        var failures = new List<string>();
        if (good.ExitCode != 0) failures.Add($"check_commit.py rejeitou uma mensagem válida (saída {good.ExitCode}).{ProcessOutput.Tail(good)}");
        if (bad.ExitCode != 1) failures.Add($"check_commit.py não rejeitou uma mensagem inválida (saída {bad.ExitCode}, esperado 1).{ProcessOutput.Tail(bad)}");
        if (self.ExitCode != 0) failures.Add($"lessons.py selftest falhou (saída {self.ExitCode}).{ProcessOutput.Tail(self)}");
        if (failures.Count > 0) return Fail(spec, "Os scripts do TLC não responderam como esperado.", failures);

        evidence.Add("check_commit.py: aceita 'feat(core): add validation' (saída 0) e rejeita uma mensagem sem tipo (saída 1)");
        evidence.Add("lessons.py selftest: saída 0");
        return Pass(spec, evidence, limitations);
    }
}
