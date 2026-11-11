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
