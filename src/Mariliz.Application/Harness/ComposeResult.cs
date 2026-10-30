using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

public sealed record ComposeResult(
    string AiEnvironment,
    string ProposalOrigin,
    IReadOnlyList<DesiredFile> Files,
    IReadOnlyList<ComponentDecision> Decisions,
    IReadOnlyList<PlannedCommand> Commands,
    IReadOnlyList<Limitation> Limitations,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<string> Risks);
