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

    Task<WorkflowAck> StartAsync(string runId, string projectId, string executionId, JsonObject input, JsonObject context, CancellationToken ct);

    Task<WorkflowAck> ResumeAsync(string runId, string projectId, string executionId, JsonObject context, CancellationToken ct);

    Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null);

    Task ShutdownAsync();
}
