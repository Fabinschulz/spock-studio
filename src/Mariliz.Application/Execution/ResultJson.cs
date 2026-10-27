using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Core.Evals;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Execution;

/// <summary>Monta o relatório final a partir do que aconteceu, nunca do que se esperava que acontecesse.</summary>
internal static class ResultJson
{
    private static JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, JournalJson.Options);

    public static JsonObject Build(RunJournal journal, ValidationReport report, ConfigurationPlan plan,
        IReadOnlyDictionary<string, ApplyService.Outcome> outcomes)
    {
        string[] Paths(Func<JournalOp, bool> predicate) => journal.Ops.Where(o => o.Source == "write" && predicate(o) && o.Path != "mariliz.lock").Select(o => o.Path).ToArray();

        var files = new JsonObject
        {
            ["created"] = Node(Paths(o => o.Op == "create")),
            ["updated"] = Node(Paths(o => o.Op == "update")),
            ["removed"] = Node(Paths(o => o.Op == "delete")),
            ["preserved"] = Node(outcomes.Where(o => o.Value.Kind == ApplyService.OutcomeKind.Preserved).Select(o => o.Key).Order().ToArray()),
            ["kept"] = Node(outcomes.Where(o => o.Value.Kind == ApplyService.OutcomeKind.Kept).Select(o => o.Key).Order().ToArray()),
            ["notApplied"] = Node(journal.NotApplied.ToArray()),
            ["commandEffects"] = Node(journal.Ops.Where(o => o.Source == "command").Select(o => $"{o.Op}: {o.Path}").ToArray())
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
            ["lockWritten"] = journal.Ops.Any(o => o.Path == "mariliz.lock"),
            ["validation"] = new JsonObject
            {
                ["outcome"] = Node(report.Outcome),
                ["results"] = Node(report.Results)
            },
            ["pending"] = Node(journal.Pending.ToArray()),
            ["limitations"] = Node(plan.Limitations),
            ["risks"] = Node(plan.Risks),
            ["steps"] = Node(journal.Steps),
            ["rollbackAvailable"] = journal.ChangesApplied && !journal.RolledBack && journal.RevertedByRunId is null
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
        ["pending"] = Node(conflicts.ToArray()),
        ["steps"] = Node(journal.Steps),
        ["rollbackAvailable"] = journal.ChangesApplied && !journal.RolledBack
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
        ["validation"] = new JsonObject { ["outcome"] = Node(report.Outcome), ["results"] = Node(report.Results) }
    };

    public static JsonObject BuildRevert(RunJournal journal, IReadOnlyList<JournalOp> reverted, IReadOnlyList<string> conflicts, bool verified) => new()
    {
        ["runId"] = journal.RunId,
        ["projectId"] = journal.ProjectId,
        ["kind"] = RunKinds.Revert,
        ["revertsRunId"] = journal.RevertsRunId,
        ["outcome"] = journal.State,
        ["reverted"] = Node(reverted.Select(o => $"{o.Op}: {o.Path}").ToArray()),
        ["conflicts"] = Node(conflicts.ToArray()),
        ["verified"] = verified,
        ["steps"] = Node(journal.Steps)
    };
}
