namespace Mariliz.Application.Harness;

public sealed record ProposalResult(bool Present, HarnessProposal? Proposal, IReadOnlyList<string> Errors);
