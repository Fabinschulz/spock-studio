using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation.Checks;

/// <summary>Existe no catálogo, mas não roda nesta fase: nenhum modelo é chamado e o juiz nunca aprova sozinho.</summary>
internal sealed class ModelJudgeCheck : IEvalCheck
{
    public string Id => EvalIds.Judge;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(EvalResults.NotRun(spec,
            "Nenhum modelo está configurado: esta fase não chama modelos de IA. O juiz semântico fica para uma fase futura."));
}
