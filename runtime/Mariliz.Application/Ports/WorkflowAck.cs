using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

public sealed record WorkflowLimitation(string Code, string Message);

/// <summary>
/// Confirmação do runtime de IA (<c>ai.ack.v2</c>). O texto livre do pedido não volta neste objeto.
/// <see cref="WorkflowId"/> junta o grafo e a versão do comportamento (<c>engineering.request.v1</c>).
/// <see cref="Limitations"/> reúne avisos e erros; <see cref="Errors"/> são só os que derrubaram a execução.
/// </summary>
public sealed record WorkflowAck(
    string WorkflowId,
    string RunId,
    string ProjectId,
    string ExecutionId,
    string Status,
    JsonObject Output,
    IReadOnlyList<WorkflowLimitation> Limitations,
    IReadOnlyList<WorkflowLimitation> Errors,
    string? InterruptId);
