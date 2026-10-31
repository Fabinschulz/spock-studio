using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal interface IEvalCheck
{
    string Id { get; }

    /// <param name="completed">Resultados dos Evals que já rodaram nesta validação.</param>
    Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct);
}
