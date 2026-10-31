using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

public sealed record WorkflowLimitation(string Code, string Message);

/// <summary>Confirmação do runtime de IA. O texto livre do pedido não volta neste objeto.</summary>
public sealed record WorkflowAck(
    string WorkflowId,
    string RunId,
    string ProjectId,
    string ExecutionId,
    string Status,
    JsonObject Output,
    IReadOnlyList<WorkflowLimitation> Limitations);
