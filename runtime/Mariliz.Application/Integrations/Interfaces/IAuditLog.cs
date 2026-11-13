using System.Text.Json.Nodes;

namespace Mariliz.Application.Integrations;

// Só acrescenta. O detalhe leva identificadores e contagens, nunca credencial ou conteúdo sincronizado.
public interface IAuditLog
{
    void Record(string action, JsonObject detail);
}

// Line é a linha do arquivo (a partir de 1): ela nunca muda de lugar.
public sealed record AuditRecord(int Line, DateTimeOffset At, string Action, JsonObject Detail);

public interface IAuditReader
{
    IReadOnlyList<AuditRecord> ReadAll();
}
