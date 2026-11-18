using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using Mariliz.Core.Policy;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class EnvironmentCheck : IEvalCheck
{
    public string Id => EvalIds.Environment;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, $"Sem {ctx.Naming.Lock}, o ambiente de IA configurado não é conhecido.");
        if (!AiEnvironments.Supported.Contains(ctx.Lock.AiEnvironment))
            return Fail(spec, $"O ambiente '{ctx.Lock.AiEnvironment}' não é suportado por esta fase.", [ctx.Naming.Lock]);

        var outside = ctx.Lock.ManagedFiles
            .Where(f => !ManagedScope.IsEnvironmentFile(f.Path))
            .Select(f => f.Path).ToList();
        return outside.Count == 0
            ? Pass(spec, [$"Ambiente {ctx.Lock.AiEnvironment}; {ctx.Lock.ManagedFiles.Count} arquivo(s) todos nas pastas e arquivos de configuração das IDEs ou em {ctx.Naming.StateDirectory}/"])
            : Fail(spec, "Há arquivos gerenciados fora das pastas do ambiente.", outside);
    }
}
