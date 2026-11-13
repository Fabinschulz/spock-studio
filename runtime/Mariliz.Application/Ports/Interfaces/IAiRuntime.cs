using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

// O motor nasce, autentica e encerra este processo; a interface e o host não falam com ele.
public interface IAiRuntime
{
    Task EnsureReadyAsync(CancellationToken ct);

    // ready se o processo responde, stopped se dá para subir, unavailable se falta o runtime. Não inicia o processo.
    string Probe();

    Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct);

    Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);

    Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);

    Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null);

    Task ShutdownAsync();
}
