using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Vocabulário de eventos do workflow para a interface. Os eventos do runtime de IA passam pelo filtro (uma vez, em ordem,
/// desta execução) e são traduzidos aqui; o que não tem tradução segura fica de fora. O carimbo de tempo é do motor.
/// </summary>
internal sealed class WorkflowEvents(IClock clock)
{
    private static readonly HashSet<string> KnownAgents = new(StringComparer.Ordinal) { "planning", "architecture" };

    /// <summary>Encaminha os eventos aceitos pelo filtro para a execução.</summary>
    /// <param name="judgeCalls">As chamadas que o proxy viu até agora: só com uma chamada de juiz o evento de avaliação por modelo passa.</param>
    public Action<JsonObject> Relay(RunHandle run, AiEventFilter filter, Func<IReadOnlyList<ModelCallRecord>>? judgeCalls = null) => evt =>
    {
        if (filter.Accept(evt)) Translate(run, evt, judgeCalls);
    };

    public JsonObject Status(string executionId, string status) => new()
    {
        ["workflowId"] = WorkflowService.WorkflowId,
        ["executionId"] = executionId,
        ["status"] = status,
        ["at"] = Now()
    };

    public void ApprovalRequired(RunHandle run, string approvalId, string executionId, JsonArray steps, JsonArray? actions = null)
    {
        run.Publish("approval.required", new JsonObject
        {
            ["approvalId"] = approvalId,
            ["executionId"] = executionId,
            ["actions"] = actions ?? new JsonArray(),
            ["at"] = Now()
        });
        var paused = Status(executionId, "interrupted");
        paused["steps"] = steps;
        run.Publish("workflow.interrupted", paused);
    }

    private void Translate(RunHandle run, JsonObject evt, Func<IReadOnlyList<ModelCallRecord>>? judgeCalls)
    {
        var data = (JsonObject)evt["data"]!;
        var agentId = evt.Text("agentId");
        switch (evt.Text("type"))
        {
            case "workflow.started":
                run.Publish("workflow.started", Status(evt.Text("executionId"), "started"));
                break;
            case "context.retrieval.started":
                run.Publish("context.retrieval.started", new JsonObject { ["at"] = Now() });
                break;
            case "context.retrieved" when WorkflowResult.TryEvidence(data, out var evidence):
                evidence["at"] = Now();
                run.Publish("context.retrieved", evidence);
                break;
            case "agent.started" when KnownAgents.Contains(agentId) && IsVersion(data.Text("agentVersion")):
                run.Publish("agent.started", new JsonObject { ["agentId"] = agentId, ["agentVersion"] = data.Text("agentVersion"), ["at"] = Now() });
                break;
            case "agent.completed" when KnownAgents.Contains(agentId) && data.Text("status") == "completed":
                run.Publish("agent.completed", new JsonObject { ["agentId"] = agentId, ["at"] = Now() });
                break;
            case "eval.started" when data.Text("evalId") == JournalJudgment.EvalId && data.Text("kind") == "model":
                run.Publish("eval.started", new JsonObject { ["evalId"] = JournalJudgment.EvalId, ["kind"] = "model", ["required"] = false, ["at"] = Now() });
                break;
            case "eval.completed" when data.Text("evalId") == JournalJudgment.EvalId && data.Text("kind") == "model":
                run.Publish("eval.completed", EvalCompleted(data, judgeCalls?.Invoke() ?? []));
                break;
        }
    }

    /// <summary>
    /// Uma avaliação por modelo só aparece como aprovada ou reprovada se o proxy viu a chamada do juiz; modelo e versão vêm do
    /// proxy. Sem isso (ou sem a versão dos critérios), o evento diz que o juiz não rodou.
    /// </summary>
    private JsonObject EvalCompleted(JsonObject data, IReadOnlyList<ModelCallRecord> calls)
    {
        var outcome = data.Text("outcome");
        var criteria = data.Text("criteriaVersion");
        var call = calls.LastOrDefault(c => c.AgentId == JudgmentVerifier.JudgeAgent && c.Purpose == "judge");
        var judged = outcome is "passed" or "failed" && call is not null && criteria.Length is > 0 and <= 9 && criteria.All(char.IsAsciiDigit);
        var evt = new JsonObject
        {
            ["evalId"] = JournalJudgment.EvalId,
            ["kind"] = "model",
            ["required"] = false,
            ["outcome"] = judged ? outcome : "not_run",
            ["at"] = Now()
        };
        if (!judged) return evt;
        evt["model"] = call!.Model;
        evt["modelVersion"] = call.ModelVersion;
        evt["criteriaVersion"] = criteria;
        return evt;
    }

    private static bool IsVersion(string value) => value.Length is > 0 and <= 9 && value.All(char.IsAsciiDigit);

    private string Now() => clock.UtcNow.ToString("O");
}
