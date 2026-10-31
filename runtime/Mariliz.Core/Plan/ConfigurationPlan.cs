namespace Mariliz.Core.Plan;

public sealed record ConfigurationPlan(
    string PlanId,
    string ProjectId,
    string AiEnvironment,
    string ImplementationModel,
    string ProposalOrigin,
    IReadOnlyList<ComponentDecision> Decisions,
    IReadOnlyList<FileChange> Changes,
    IReadOnlyList<PlannedCommand> Commands,
    IReadOnlyList<Limitation> Limitations,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Validations)
{
    public bool IsEmpty =>
        Commands.Count == 0 && Changes.All(c => c.Action == ChangeAction.Preserve);
}
