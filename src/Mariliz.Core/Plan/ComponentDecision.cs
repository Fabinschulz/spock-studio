namespace Mariliz.Core.Plan;

public sealed record ComponentDecision(
    ComponentKind Kind,
    string ComponentId,
    string Decision,
    string Justification,
    string Origin,
    IReadOnlyList<string> Evidence);
