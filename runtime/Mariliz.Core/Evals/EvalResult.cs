namespace Mariliz.Core.Evals;

public sealed record EvalResult(
    string Id,
    string Name,
    string Property,
    EvalKind Kind,
    bool Required,
    EvalStatus Status,
    string Criteria,
    IReadOnlyList<string> Evidence,
    string? Reason,
    IReadOnlyList<string> Limitations);
