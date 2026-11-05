using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Mensagens do motor para o runtime de IA (<c>ai.workflow.start.v2</c> e <c>ai.workflow.resume.v2</c>). Limites e
/// política são decisões do motor; o runtime só pode ser mais restritivo.
/// </summary>
internal static class WorkflowRequests
{
    public const string GraphId = "engineering.request";
    public const string GraphVersion = "1";

    /// <summary>
    /// Teto de tokens de uma execução com modelo (D4, decidida em 2026-11-05): soma de entrada e saída, cobrada pelo proxy.
    /// Sem teto em dinheiro. O tempo sobe junto, porque uma chamada de modelo leva segundos.
    /// </summary>
    public const int ModelTokenBudget = 8000;
    public const int ModelMilliseconds = 60_000;

    public static JsonObject Start(RunHandle run, string executionId, JsonObject input, JsonObject context, ModelGrant? model = null)
    {
        var message = new JsonObject
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
            ["limits"] = new JsonObject
            {
                ["maxIterations"] = 8,
                ["maxMilliseconds"] = model is null ? 10_000 : ModelMilliseconds,
                ["maxTokens"] = model?.TokenBudget ?? 0,
                ["maxCostUsd"] = 0
            },
            ["policy"] = new JsonObject { ["allowedActions"] = new JsonArray(), ["modelAllowed"] = model is not null }
        };
        if (model is not null) message["broker"] = new JsonObject { ["url"] = model.Url, ["token"] = model.Token };
        return message;
    }

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
