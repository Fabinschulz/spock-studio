namespace Mariliz.Application.Harness;

public sealed record ProposalDecision(string Id, string Decision, string Justification, IReadOnlyList<string> Evidence);
