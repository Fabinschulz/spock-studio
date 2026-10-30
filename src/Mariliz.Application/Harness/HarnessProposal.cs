using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

public sealed record HarnessProposal(
    IReadOnlyList<ProposalDecision> Decisions,
    IReadOnlyList<ProposalRule> AdditionalRules,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<string> Risks);
