using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class ConfigCheck : IEvalCheck
{
    public string Id => EvalIds.Config;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        var evidence = new List<string>();
        if (ctx.DeclaredError is not null) return Fail(spec, ctx.DeclaredError, [DeclaredConfig.FileName]);
        if (ctx.Lock is null)
            return ctx.Files.FileExists(ManagedLockFile.FileName)
                ? Fail(spec, "mariliz.lock existe, mas não é um YAML válido.", [ManagedLockFile.FileName])
                : Block(spec, "mariliz.lock não existe: nenhuma configuração foi aplicada neste projeto.");

        if (ctx.Lock.Schema != 1) return Fail(spec, $"mariliz.lock com schema {ctx.Lock.Schema}; esperado 1.", [ManagedLockFile.FileName]);
        if (string.IsNullOrWhiteSpace(ctx.Lock.Engine)) return Fail(spec, "mariliz.lock sem a versão do motor.", [ManagedLockFile.FileName]);
        if (!AiEnvironments.Known.Contains(ctx.Lock.AiEnvironment)) return Fail(spec, "mariliz.lock declara um ambiente de IA desconhecido.", [ManagedLockFile.FileName]);
        evidence.Add($"{ManagedLockFile.FileName}: schema 1, motor {ctx.Lock.Engine}, ambiente {ctx.Lock.AiEnvironment}, {ctx.Lock.ManagedFiles.Count} arquivo(s)");

        if (ctx.Declared is not null)
        {
            if (ctx.Declared.AiEnvironment.Length > 0 && ctx.Declared.AiEnvironment != ctx.Lock.AiEnvironment)
                return Fail(spec, $"mariliz.yaml declara '{ctx.Declared.AiEnvironment}', mas o lock registra '{ctx.Lock.AiEnvironment}'.", [DeclaredConfig.FileName, ManagedLockFile.FileName]);
            evidence.Add($"{DeclaredConfig.FileName}: schema 1, method.sdd=tlc");
        }
        return Pass(spec, evidence);
    }
}
