using System.Text.Json.Nodes;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

/// <summary>Monta o relatório final a partir do que aconteceu, nunca do que se esperava que acontecesse.</summary>
internal static class ResultJson
{
    public static JsonObject Build(RunJournal journal, ValidationReport report, ConfigurationPlan plan,
        IReadOnlyDictionary<string, FileOutcome> outcomes)
    {
        string[] Paths(Func<JournalOp, bool> predicate) => journal.Ops.Where(o => o.Source == OpSources.Write && predicate(o) && o.Path != ManagedLockFile.FileName).Select(o => o.Path).ToArray();

        var files = new JsonObject
        {
            ["created"] = JournalJson.Node(Paths(o => o.Op == OpKinds.Create)),
            ["updated"] = JournalJson.Node(Paths(o => o.Op == OpKinds.Update)),
            ["removed"] = JournalJson.Node(Paths(o => o.Op == OpKinds.Delete)),
            ["preserved"] = JournalJson.Node(outcomes.Where(o => o.Value.Kind == OutcomeKind.Preserved).Select(o => o.Key).Order().ToArray()),
            ["kept"] = JournalJson.Node(outcomes.Where(o => o.Value.Kind == OutcomeKind.Kept).Select(o => o.Key).Order().ToArray()),
            ["notApplied"] = JournalJson.Node(journal.NotApplied.ToArray()),
            ["commandEffects"] = JournalJson.Node(journal.Ops.Where(o => o.Source == OpSources.Command).Select(o => $"{o.Op}: {o.Path}").ToArray())
        };

        return new JsonObject
        {
            ["runId"] = journal.RunId,
            ["projectId"] = journal.ProjectId,
            ["kind"] = journal.Kind,
            ["planId"] = journal.PlanId,
            ["outcome"] = journal.State,
            ["changesApplied"] = journal.ChangesApplied,
            ["rolledBack"] = journal.RolledBack,
            ["files"] = files,
            ["lockWritten"] = journal.Ops.Any(o => o.Path == ManagedLockFile.FileName),
            ["validation"] = new JsonObject
            {
                ["outcome"] = JournalJson.Node(report.Outcome),
                ["results"] = JournalJson.Node(report.Results)
            },
            ["pending"] = JournalJson.Node(journal.Pending.ToArray()),
            ["limitations"] = JournalJson.Node(plan.Limitations),
            ["risks"] = JournalJson.Node(plan.Risks),
            ["steps"] = JournalJson.Node(journal.Steps),
            ["rollbackAvailable"] = journal.CanBeReverted
        };
    }

    public static JsonObject BuildFailure(RunJournal journal, string message, string? code, IReadOnlyList<string> conflicts) => new()
    {
        ["runId"] = journal.RunId,
        ["projectId"] = journal.ProjectId,
        ["kind"] = journal.Kind,
        ["planId"] = journal.PlanId,
        ["outcome"] = journal.State,
        ["changesApplied"] = journal.ChangesApplied,
        ["rolledBack"] = journal.RolledBack,
        ["error"] = new JsonObject { ["code"] = code ?? (journal.State == RunStates.Cancelled ? "CANCELLED" : "FAILED"), ["message"] = message },
        ["pending"] = JournalJson.Node(conflicts.ToArray()),
        ["steps"] = JournalJson.Node(journal.Steps),
        ["rollbackAvailable"] = journal.CanBeReverted
    };

    public static JsonObject BuildValidation(string runId, string projectId, ValidationReport report) => new()
    {
        ["runId"] = runId,
        ["projectId"] = projectId,
        ["kind"] = RunKinds.Validate,
        ["outcome"] = report.Outcome switch
        {
            ValidationOutcome.Failed => RunStates.Failed,
            ValidationOutcome.PassedWithLimitations => RunStates.CompletedWithLimitations,
            _ => RunStates.Completed
        },
        ["validation"] = new JsonObject { ["outcome"] = JournalJson.Node(report.Outcome), ["results"] = JournalJson.Node(report.Results) }
    };

    public static JsonObject BuildRevert(RunJournal journal, IReadOnlyList<JournalOp> reverted, IReadOnlyList<string> conflicts, bool verified) => new()
    {
        ["runId"] = journal.RunId,
        ["projectId"] = journal.ProjectId,
        ["kind"] = RunKinds.Revert,
        ["revertsRunId"] = journal.RevertsRunId,
        ["outcome"] = journal.State,
        ["reverted"] = JournalJson.Node(reverted.Select(o => $"{o.Op}: {o.Path}").ToArray()),
        ["conflicts"] = JournalJson.Node(conflicts.ToArray()),
        ["verified"] = verified,
        ["steps"] = JournalJson.Node(journal.Steps)
    };
}
