using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

/// <summary>Liga os eventos das execuções ao fluxo de saída da sessão, que só existe depois de a sessão ser criada.</summary>
public sealed class EventRelay
{
    public Action<string, int, string, JsonObject, DateTimeOffset>? Target { get; set; }

    public void Emit(string runId, int seq, string type, JsonObject data, DateTimeOffset at) => Target?.Invoke(runId, seq, type, data, at);
}
