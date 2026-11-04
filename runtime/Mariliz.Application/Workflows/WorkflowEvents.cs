using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Vocabulário de eventos do workflow para a interface. Os eventos do runtime de IA passam pelo filtro (uma vez, em ordem,
/// desta execução) e são traduzidos aqui; o que não tem tradução segura fica de fora. O carimbo de tempo é do motor.
/// </summary>
internal sealed class WorkflowEvents(IClock clock)
{
    private static readonly HashSet<string> KnownAgents = new(StringComparer.Ordinal) { "architecture" };

    /// <summary>Encaminha os eventos aceitos pelo filtro para a execução.</summary>
    public Action<JsonObject> Relay(RunHandle run, AiEventFilter filter) => evt =>
    {
        if (filter.Accept(evt)) Translate(run, evt);
    };

    public JsonObject Status(string executionId, string status) => new()
    {
        ["workflowId"] = WorkflowService.WorkflowId,
        ["executionId"] = executionId,
        ["status"] = status,
        ["at"] = Now()
    };

    public void ApprovalRequired(RunHandle run, string approvalId, string executionId, JsonArray steps)
    {
        run.Publish("approval.required", new JsonObject
        {
            ["approvalId"] = approvalId,
            ["executionId"] = executionId,
            ["actions"] = new JsonArray(),
            ["at"] = Now()
        });
        var paused = Status(executionId, "interrupted");
        paused["steps"] = steps;
        run.Publish("workflow.interrupted", paused);
    }

    private void Translate(RunHandle run, JsonObject evt)
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
        }
    }

    private static bool IsVersion(string value) => value.Length is > 0 and <= 9 && value.All(char.IsAsciiDigit);

    private string Now() => clock.UtcNow.ToString("O");
}
