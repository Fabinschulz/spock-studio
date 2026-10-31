namespace Mariliz.Application.Execution;

internal sealed record WriteDecision(
    IReadOnlyList<PlannedWrite> Writes,
    Dictionary<string, FileOutcome> Outcomes,
    IReadOnlyList<string> NotApplied);
