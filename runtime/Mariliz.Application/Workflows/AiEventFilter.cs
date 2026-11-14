using System.Text.Json.Nodes;
using Mariliz.Application.Protocol;

namespace Mariliz.Application.Workflows;

// Evento repetido (mesmo eventId) ou atrasado (localSeq que já passou) é descartado.
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
