using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Core.Discovery;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class SpecificationCheck : IEvalCheck
{
    public string Id => EvalIds.Specification;

    public async Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct)
    {
        var specification = SpecificationInspector.Inspect(ctx.Files);
        var evidence = new List<string>();
        var limitations = new List<string>();

        if (specification.State == SpecificationState.NotInstalled) return Block(spec, "A estrutura de especificação não está instalada neste projeto.");
        if (specification.State is SpecificationState.Invalid or SpecificationState.Drifted)
            return Fail(spec, specification.State == SpecificationState.Invalid ? "A estrutura de especificação está incompleta ou inválida." : "A estrutura de especificação difere do hash do instalador.", specification.Problems);

        evidence.Add($"{SpecificationManifest.SkillDirectory}: {SpecificationManifest.RequiredFiles.Count} arquivos exigidos presentes");
        evidence.Add($"Hash conferido com {SpecificationManifest.LockFile} ({specification.LockedHash![..12]}…)");
        if (!specification.VersionTested) limitations.Add($"A versão {specification.Version ?? "desconhecida"} da estrutura de especificação não foi validada por esta fase (validada: {string.Join(", ", SpecificationManifest.TestedVersions)}).");
        else evidence.Add($"Versão {specification.Version} validada por esta fase");

        var python = ctx.Discovery.Tools.PythonCommand;
        if (python is null) return Block(spec, "Python 3 não está disponível; as validações da especificação não puderam ser executadas.");

        var scripts = Path.Combine(ctx.RootPath, SpecificationManifest.SkillDirectory.Replace('/', Path.DirectorySeparatorChar), "scripts");
        var good = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "feat(core): add validation"], ctx.RootPath, EvalTimeouts.Script), ct);
        var bad = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "check_commit.py"), "--message", "Mensagem sem tipo"], ctx.RootPath, EvalTimeouts.Script), ct);
        var self = await ctx.Runner.RunAsync(new ProcessSpec(python, [Path.Combine(scripts, "lessons.py"), "selftest"], ctx.RootPath, EvalTimeouts.Script), ct);

        // Não rodar ou estourar o tempo não é uma resposta errada: é uma verificação que não pôde ser concluída.
        var notRun = new[] { ("check_commit.py (mensagem válida)", good), ("check_commit.py (mensagem inválida)", bad), ("lessons.py selftest", self) }
            .Where(x => x.Item2.TimedOut || x.Item2.NotFound)
            .Select(x => $"{x.Item1}: {(x.Item2.TimedOut ? $"excedeu {EvalTimeouts.Script.TotalSeconds:0} s" : "não pôde ser executado")}.{ProcessOutput.Tail(x.Item2)}")
            .ToList();
        if (notRun.Count > 0) return Make(spec, EvalStatus.Blocked, notRun, "Os scripts de validação não puderam ser executados até o fim; as validações da especificação não foram verificadas.", []);

        var failures = new List<string>();
        if (good.ExitCode != 0) failures.Add($"check_commit.py rejeitou uma mensagem válida (saída {good.ExitCode}).{ProcessOutput.Tail(good)}");
        if (bad.ExitCode != 1) failures.Add($"check_commit.py não rejeitou uma mensagem inválida (saída {bad.ExitCode}, esperado 1).{ProcessOutput.Tail(bad)}");
        if (self.ExitCode != 0) failures.Add($"lessons.py selftest falhou (saída {self.ExitCode}).{ProcessOutput.Tail(self)}");
        if (failures.Count > 0) return Fail(spec, "Os scripts de validação não responderam como esperado.", failures);

        evidence.Add("check_commit.py: aceita 'feat(core): add validation' (saída 0) e rejeita uma mensagem sem tipo (saída 1)");
        evidence.Add("lessons.py selftest: saída 0");
        return Pass(spec, evidence, limitations);
    }
}
