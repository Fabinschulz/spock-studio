using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

internal interface IEvalCheck
{
    string Id { get; }

    Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct);
}
