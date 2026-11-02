using System.Text.Json.Nodes;

namespace Mariliz.Application.Ports;

/// <summary>
/// Processo local de IA. O motor nasce, autentica e encerra este processo;
/// a interface e o host não falam com ele.
/// </summary>
public interface IAiRuntime
{
    Task EnsureReadyAsync(CancellationToken ct);

    /// <summary>ready se o processo responde, stopped se dá para subir, unavailable se falta o runtime. Não inicia o processo.</summary>
    string Probe();

    Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct);

    /// <summary>Envia um <c>ai.workflow.start.v2</c>. Cada <c>ai.event.v1</c> chega em <paramref name="onEvent"/> enquanto o grafo anda.</summary>
    Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);

    /// <summary>Envia um <c>ai.workflow.resume.v2</c> com a aprovação que o motor já gravou.</summary>
    Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);

    Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null);

    Task ShutdownAsync();
}
