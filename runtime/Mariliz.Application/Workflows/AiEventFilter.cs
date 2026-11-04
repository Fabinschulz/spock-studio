using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Porta de entrada dos <c>ai.event.v1</c> de uma execução: só passa o que é desta execução, uma vez e em ordem.
/// Um evento repetido (mesmo <c>eventId</c>) ou atrasado (<c>localSeq</c> que já passou) é descartado.
/// </summary>
internal sealed class AiEventFilter(string executionId)
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private long _lastSeq = -1;

    public bool Accept(JsonObject evt)
    {
        if (evt.Text("schema") != "ai.event.v1" || evt.Text("executionId") != executionId) return false;
        if (evt["localSeq"] is not JsonValue value || !long.TryParse(value.ToJsonString(), out var seq) || seq < 0) return false;
        var eventId = evt.Text("eventId");
        if (eventId != $"{executionId}:{seq}" || evt["data"] is not JsonObject) return false;

        lock (_seen)
        {
            if (seq <= _lastSeq || !_seen.Add(eventId)) return false;
            _lastSeq = seq;
            return true;
        }
    }
}
