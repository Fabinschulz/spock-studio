using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class EnvironmentCheck : IEvalCheck
{
    public string Id => EvalIds.Environment;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, o ambiente de IA configurado não é conhecido.");
        if (!AiEnvironments.Supported.Contains(ctx.Lock.AiEnvironment))
            return Fail(spec, $"O ambiente '{ctx.Lock.AiEnvironment}' não é suportado por esta fase.", [ManagedLockFile.FileName]);

        var outside = ctx.Lock.ManagedFiles
            .Where(f => !(f.Path.StartsWith(".claude/", StringComparison.Ordinal) || f.Path.StartsWith(".mariliz/", StringComparison.Ordinal) || f.Path is DeclaredConfig.FileName or ManagedLockFile.FileName))
            .Select(f => f.Path).ToList();
        return outside.Count == 0
            ? Pass(spec, [$"Ambiente claude-code; {ctx.Lock.ManagedFiles.Count} arquivo(s) todos em .claude/, .mariliz/ ou na raiz de configuração"])
            : Fail(spec, "Há arquivos gerenciados fora das pastas do ambiente.", outside);
    }
}
