using Mariliz.Application.Harness;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

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
            var effect = EffectOf(change, approvals.ChangeIds.Contains(change.Id), approvals);
            if (effect.Write is { } write) writes.Add(write);
            if (effect.Outcome is { } outcome) outcomes[change.Path] = outcome;
            if (effect.Skipped is { } path) notApplied.Add(path);
        }

        return new WriteDecision(writes, outcomes, notApplied);
    }

    private readonly record struct ChangeEffect(PlannedWrite? Write, FileOutcome? Outcome, string? Skipped)
    {
        public static ChangeEffect WriteOf(PlannedWrite write) => new(write, null, null);
        public static ChangeEffect OutcomeOf(FileOutcome outcome) => new(null, outcome, null);
        public static ChangeEffect SkippedOf(string path, FileOutcome? outcome) => new(null, outcome, path);
    }

    private static ChangeEffect EffectOf(FileChange change, bool approved, ApplyApprovals approvals) => change.Action switch
    {
        ChangeAction.Preserve => ChangeEffect.OutcomeOf(change.Origin == DecisionOrigins.Existing
            ? new FileOutcome(OutcomeKind.Kept, change.BeforeHash)
            : new FileOutcome(OutcomeKind.Preserved, change.AfterHash ?? change.BeforeHash)),

        ChangeAction.Create or ChangeAction.Update when change.Path != ManagedLockFile.FileName => approved
            ? ChangeEffect.WriteOf(new PlannedWrite(change, change.Action == ChangeAction.Create ? OpKinds.Create : OpKinds.Update))
            : ChangeEffect.SkippedOf(change.Path, new FileOutcome(OutcomeKind.NotApplied, change.BeforeHash)),

        ChangeAction.Remove => approved
            ? ChangeEffect.WriteOf(new PlannedWrite(change, OpKinds.Delete))
            : ChangeEffect.SkippedOf(change.Path, null),

        ChangeAction.Conflict when approved && approvals.Resolutions.GetValueOrDefault(change.Id, ConflictResolution.Keep) == ConflictResolution.Overwrite =>
            ChangeEffect.WriteOf(new PlannedWrite(change, change.AfterHash is null ? OpKinds.Delete : OpKinds.Update)),

        ChangeAction.Conflict => ChangeEffect.OutcomeOf(new FileOutcome(OutcomeKind.Kept, change.BeforeHash)),

        _ => default
    };
}
