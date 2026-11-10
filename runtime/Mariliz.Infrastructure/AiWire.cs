using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Core.Text;

namespace Mariliz.Infrastructure;

/// <summary>Leitura do <c>ai.ack.v2</c> que o runtime de IA devolve: o que sai do JSON da conversa e entra no vocabulário do motor.</summary>
internal static class AiWire
{
    public static WorkflowAck ReadAck(JsonObject ack)
    {
        var warnings = ReadNotices(ack, "warnings");
        var errors = ReadNotices(ack, "errors");
        var interrupt = ack["approval"] is JsonObject approval ? approval.Text("interruptId") : "";
        return new WorkflowAck(
            $"{ack.Text("workflowId")}.v{ack.Text("workflowVersion")}",
            ack.Text("runId"),
            ack.Text("projectId"),
            ack.Text("executionId"),
            ack.Text("status"),
            ack["output"] as JsonObject ?? new JsonObject(),
            [.. warnings, .. errors],
            errors,
            Ids.IsInterruptId(interrupt) ? interrupt : null,
            ack["nextActions"] is JsonArray proposals ? proposals.OfType<JsonObject>().Select(p => (JsonObject)p.DeepClone()).ToList() : []);
    }

    private static IReadOnlyList<WorkflowLimitation> ReadNotices(JsonObject ack, string name)
    {
        if (ack[name] is not JsonArray array) return [];
        var items = new List<WorkflowLimitation>();
        foreach (var node in array)
        {
            if (node is not JsonObject item) continue;
            var code = item.Text("code");
            if (code.Length > 0) items.Add(new WorkflowLimitation(code, item.Text("message")));
        }
        return items;
    }
}
