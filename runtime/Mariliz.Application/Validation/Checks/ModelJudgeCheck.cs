using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation.Checks;

/// <summary>
/// O juiz consulta se há modelo. Sem modelo, não roda. Com modelo, também não aprova sozinho:
/// o resultado da configuração continua nas verificações determinísticas.
/// </summary>
internal sealed class ModelJudgeCheck : IEvalCheck
{
    public string Id => EvalIds.Judge;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(ctx.Models?.Status() switch
        {
            { Configured: true } => EvalResults.NotRun(spec, "O juiz por modelo não decide o resultado."),
            _ => EvalResults.NotRun(spec, "Nenhum modelo está configurado: esta fase não chama modelos de IA. O juiz semântico fica para uma fase futura.")
        });
}
