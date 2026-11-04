using System.Text.Json.Nodes;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Mensagens do motor para o runtime de IA (<c>ai.workflow.start.v2</c> e <c>ai.workflow.resume.v2</c>). Limites e
/// política são decisões do motor; o runtime só pode ser mais restritivo.
/// </summary>
internal static class WorkflowRequests
{
    public const string GraphId = "engineering.request";
    public const string GraphVersion = "1";

    public static JsonObject Start(RunHandle run, string executionId, JsonObject input, JsonObject context) => new()
    {
        ["schema"] = "ai.workflow.start.v2",
        ["workflowId"] = GraphId,
        ["workflowVersion"] = GraphVersion,
        ["runId"] = run.RunId,
        ["executionId"] = executionId,
        ["projectId"] = run.ProjectId,
        ["workspaceId"] = null,
        ["input"] = input.DeepClone(),
        ["context"] = context,
        ["limits"] = new JsonObject { ["maxIterations"] = 8, ["maxMilliseconds"] = 10_000, ["maxTokens"] = 0, ["maxCostUsd"] = 0 },
        ["policy"] = new JsonObject { ["allowedActions"] = new JsonArray(), ["modelAllowed"] = false }
    };

    /// <summary>Retomada com a aprovação que o motor já gravou no diário.</summary>
    public static JsonObject Resume(RunHandle run, RunJournal journal, string executionRoot) => new()
    {
        ["schema"] = "ai.workflow.resume.v2",
        ["runId"] = run.RunId,
        ["executionId"] = journal.ExecutionId,
        ["projectId"] = run.ProjectId,
        ["interruptId"] = journal.InterruptId,
        ["decision"] = new JsonObject { ["approvalId"] = journal.ApprovalId, ["approved"] = true, ["actionIds"] = new JsonArray() },
        ["context"] = new JsonObject { ["executionRoot"] = executionRoot }
    };
}
