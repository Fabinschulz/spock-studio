using System.Text.Json.Nodes;

namespace Mariliz.Application.Integrations;

/// <summary>
/// Trilha de operações que mudam o que o Mariliz pode acessar: conexões, recortes e sincronizações. Só acrescenta.
/// O detalhe leva identificadores e contagens, nunca credencial ou conteúdo sincronizado.
/// </summary>
public interface IAuditLog
{
    void Record(string action, JsonObject detail);
}

/// <summary>Uma operação da trilha como foi gravada. <see cref="Line"/> é a linha do arquivo (a partir de 1): ela nunca muda de lugar.</summary>
public sealed record AuditRecord(int Line, DateTimeOffset At, string Action, JsonObject Detail);

/// <summary>Lê a trilha de auditoria. Só leitura: a trilha continua só acrescentando.</summary>
public interface IAuditReader
{
    IReadOnlyList<AuditRecord> ReadAll();
}
