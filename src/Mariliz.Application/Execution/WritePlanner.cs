using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

internal sealed record PlannedWrite(FileChange Change, string Op);

internal sealed record WriteDecision(
    IReadOnlyList<PlannedWrite> Writes,
    Dictionary<string, FileOutcome> Outcomes,
    IReadOnlyList<string> NotApplied);

/// <summary>Transforma o plano e as aprovações em operações de arquivo. O arquivo de lock é tratado à parte.</summary>
internal static class WritePlanner
{
    public static WriteDecision Decide(ConfigurationPlan plan, ApplyApprovals approvals)
    {
        var writes = new List<PlannedWrite>();
        var outcomes = new Dictionary<string, FileOutcome>(StringComparer.Ordinal);
        var notApplied = new List<string>();

        foreach (var change in plan.Changes)
        {
            var approved = approvals.ChangeIds.Contains(change.Id);

            switch (change.Action)
            {
                case ChangeAction.Preserve:
                    outcomes[change.Path] = change.Origin == DecisionOrigins.Existing
                        ? new FileOutcome(OutcomeKind.Kept, change.BeforeHash)
                        : new FileOutcome(OutcomeKind.Preserved, change.AfterHash ?? change.BeforeHash);
                    break;

                case ChangeAction.Create or ChangeAction.Update when change.Path != ManagedLockFile.FileName:
                    if (approved)
                        writes.Add(new PlannedWrite(change, change.Action == ChangeAction.Create ? OpKinds.Create : OpKinds.Update));
                    else
                    {
                        notApplied.Add(change.Path);
                        outcomes[change.Path] = new FileOutcome(OutcomeKind.NotApplied, change.BeforeHash);
                    }
                    break;

                case ChangeAction.Remove:
                    if (approved) writes.Add(new PlannedWrite(change, OpKinds.Delete));
                    else notApplied.Add(change.Path);
                    break;

                case ChangeAction.Conflict:
                    var resolution = approvals.Resolutions.GetValueOrDefault(change.Id, ConflictResolution.Keep);
                    if (approved && resolution == ConflictResolution.Overwrite)
                        writes.Add(new PlannedWrite(change, change.AfterHash is null ? OpKinds.Delete : OpKinds.Update));
                    else
                        outcomes[change.Path] = new FileOutcome(OutcomeKind.Kept, change.BeforeHash);
                    break;
            }
        }

        return new WriteDecision(writes, outcomes, notApplied);
    }
}
