using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

// O fluxo de saída só existe depois de a sessão ser criada.
public sealed class EventRelay
{
    public Action<string, int, string, JsonObject, DateTimeOffset>? Target { get; set; }

    public void Emit(string runId, int seq, string type, JsonObject data, DateTimeOffset at) => Target?.Invoke(runId, seq, type, data, at);
}
