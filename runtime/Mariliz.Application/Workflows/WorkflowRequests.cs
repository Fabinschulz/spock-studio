using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Application.Workflows;

// Limites e política são decisões do motor; o runtime só pode ser mais restritivo.
internal static class WorkflowRequests
{
    public const string GraphId = "engineering.request";
    public const string GraphVersion = "1";

    // O tempo em que o runtime ainda começa uma chamada ao modelo (o runtime espelha esse teto em MAX_MILLISECONDS_MODEL). Depois dele ele
    // segue só com o que é determinístico, e a chamada que já estava em andamento termina.
    public static readonly TimeSpan RuntimeBudget = TimeSpan.FromSeconds(90);

    // O prazo da conversa com o runtime: o tempo de começar chamadas mais uma chamada que comece no fim dele (o prazo do chat) e uma folga.
    // Menor que isso o motor desistia no meio de uma resposta de modelo lento, e a execução falhava sem resultado em vez de seguir com o plano
    // determinístico.
    public static readonly TimeSpan StandardDeadline = RuntimeBudget + ModelTimeouts.Chat + TimeSpan.FromSeconds(15);

    public static int ModelMilliseconds => (int)RuntimeBudget.TotalMilliseconds;

    public static JsonObject Start(RunHandle run, string executionId, JsonObject input, JsonObject context, ModelGrant? model = null,
        IReadOnlyList<string>? allowedActions = null)
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
            ["policy"] = new JsonObject
            {
                ["allowedActions"] = new JsonArray((allowedActions ?? []).Select(kind => (JsonNode)kind).ToArray()),
                ["modelAllowed"] = model is not null
            }
        };
        if (model is not null) message["broker"] = new JsonObject { ["url"] = model.Url, ["token"] = model.Token };
        return message;
    }

    public static JsonObject Resume(RunHandle run, RunJournal journal, string executionRoot) => new()
    {
        ["schema"] = "ai.workflow.resume.v2",
        ["runId"] = run.RunId,
        ["executionId"] = journal.ExecutionId,
        ["projectId"] = run.ProjectId,
        ["interruptId"] = journal.InterruptId,
        ["decision"] = new JsonObject
        {
            ["approvalId"] = journal.ApprovalId,
            ["approved"] = true,
            ["actionIds"] = new JsonArray(journal.Actions.Where(a => a.Status == JournalAction.Approved).Select(a => (JsonNode)a.Id).ToArray())
        },
        ["context"] = new JsonObject { ["executionRoot"] = executionRoot }
    };
}
