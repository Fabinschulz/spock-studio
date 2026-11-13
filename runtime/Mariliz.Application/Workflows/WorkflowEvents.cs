using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

// O que não tem tradução segura fica de fora. O carimbo de tempo é do motor.
internal sealed class WorkflowEvents(IClock clock)
{
    private static readonly HashSet<string> KnownAgents = new(StringComparer.Ordinal) { "planning", "architecture" };

    /// <param name="judgeCalls">Só com uma chamada de juiz vista pelo proxy o evento de avaliação por modelo passa.</param>
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
        var type = evt.Text("type");
        JsonObject? payload = type switch
        {
            "workflow.started" => Status(evt.Text("executionId"), "started"),
            "context.retrieval.started" => Stamped(new JsonObject()),
            "context.retrieved" when WorkflowResult.TryEvidence(data, out var evidence) => Stamped(evidence),
            "agent.started" when KnownAgents.Contains(agentId) && IsVersion(data.Text("agentVersion")) =>
                Stamped(new JsonObject { ["agentId"] = agentId, ["agentVersion"] = data.Text("agentVersion") }),
            "agent.completed" when KnownAgents.Contains(agentId) && data.Text("status") == "completed" =>
                Stamped(new JsonObject { ["agentId"] = agentId }),
            "eval.started" when IsModelJudge(data) =>
                Stamped(new JsonObject { ["evalId"] = JournalJudgment.EvalId, ["kind"] = "model", ["required"] = false }),
            "eval.completed" when IsModelJudge(data) => EvalCompleted(data, judgeCalls?.Invoke() ?? []),
            _ => null
        };
        if (payload is not null) run.Publish(type, payload);
    }

    private static bool IsModelJudge(JsonObject data) => data.Text("evalId") == JournalJudgment.EvalId && data.Text("kind") == "model";

    private JsonObject Stamped(JsonObject payload)
    {
        payload["at"] = Now();
        return payload;
    }

    // Modelo e versão vêm do proxy; sem a chamada do juiz (ou sem a versão dos critérios), o evento diz que o juiz não rodou.
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
