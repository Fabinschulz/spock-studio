using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

public sealed record WorkflowLimitation(string Code, string Message);

// Conjunto fechado: o que não for um destes é resposta fora do contrato.
public enum AckStatus
{
    Completed,
    Interrupted,
    Failed
}

public static class AckStatusWire
{
    public static AckStatus? Parse(string wire) => wire switch
    {
        "completed" => AckStatus.Completed,
        "interrupted" => AckStatus.Interrupted,
        "failed" => AckStatus.Failed,
        _ => null
    };

    public static string ToWire(this AckStatus status) => status switch
    {
        AckStatus.Completed => "completed",
        AckStatus.Interrupted => "interrupted",
        _ => "failed"
    };
}

// O texto livre do pedido não volta neste objeto. Limitations reúne avisos e erros; Errors são só os que derrubaram a execução.
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
    // Quem escolhe o alvo, decide e executa é o motor.
    public IReadOnlyList<JsonObject> Actions => NextActions ?? [];
}
