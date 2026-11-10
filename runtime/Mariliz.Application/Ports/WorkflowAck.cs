using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

public sealed record WorkflowLimitation(string Code, string Message);

/// <summary>Como o runtime de IA terminou uma conversa. É um conjunto fechado: o que não for um destes é resposta fora do contrato.</summary>
public enum AckStatus
{
    Completed,
    Interrupted,
    Failed
}

public static class AckStatusWire
{
    /// <summary>O valor de <c>status</c> no <c>ai.ack.v3</c>, ou <c>null</c> se não for um dos três.</summary>
    public static AckStatus? Parse(string wire) => wire switch
    {
        "completed" => AckStatus.Completed,
        "interrupted" => AckStatus.Interrupted,
        "failed" => AckStatus.Failed,
        _ => null
    };

    /// <summary>O mesmo valor, como vai nos eventos do motor.</summary>
    public static string ToWire(this AckStatus status) => status switch
    {
        AckStatus.Completed => "completed",
        AckStatus.Interrupted => "interrupted",
        _ => "failed"
    };
}

/// <summary>
/// Confirmação do runtime de IA (<c>ai.ack.v3</c>). O texto livre do pedido não volta neste objeto.
/// <see cref="WorkflowId"/> junta o grafo e a versão do comportamento (<c>engineering.request.v1</c>).
/// <see cref="Limitations"/> reúne avisos e erros; <see cref="Errors"/> são só os que derrubaram a execução.
/// </summary>
public sealed record WorkflowAck(
    string WorkflowId,
    string RunId,
    string ProjectId,
    string ExecutionId,
    AckStatus Status,
    JsonObject Output,
    IReadOnlyList<WorkflowLimitation> Limitations,
    IReadOnlyList<WorkflowLimitation> Errors,
    string? InterruptId,
    IReadOnlyList<JsonObject>? NextActions = null)
{
    /// <summary>As intenções que o runtime propôs. Quem escolhe o alvo, decide e executa é o motor.</summary>
    public IReadOnlyList<JsonObject> Actions => NextActions ?? [];
}
