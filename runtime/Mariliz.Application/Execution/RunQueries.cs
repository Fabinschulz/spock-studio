using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>
/// Consulta e cancelamento de qualquer execução (aplicação, validação, reversão ou workflow), independentes da jornada
/// que a iniciou.
/// </summary>
public sealed class RunQueries(RunTracker tracker, IRunStore store, RunCatalog? catalog = null)
{
    /// <summary>
    /// Com <paramref name="sinceSeq"/>, devolve todos os eventos depois desse número: os que a memória já descartou vêm do
    /// histórico em disco, para a interface recuperar o que perdeu numa reconexão.
    /// </summary>
    public JsonObject Snapshot(string runId, int? sinceSeq = null, string? projectId = null)
    {
        var run = tracker.Find(runId);
        // Uma execução de outro projeto, ou de um projeto que não está autorizado, é "não encontrada": o motor não confirma
        // que ela existe para quem não deveria vê-la.
        if (run is null || !Visible(run.ProjectId, projectId)) throw EngineErrors.RunNotFound();
        var snapshot = run.Snapshot(sinceSeq);
        if (sinceSeq is not { } since || snapshot["firstSeq"]!.GetValue<int>() <= since + 1) return snapshot;

        var retained = snapshot["events"]!.AsArray();
        var firstRetained = snapshot["firstSeq"]!.GetValue<int>();
        var older = store.LoadEvents(run.ProjectId, run.RunId)
            .Where(e => e["seq"] is JsonValue seq && seq.GetValue<int>() > since && seq.GetValue<int>() < firstRetained);
        snapshot["events"] = new JsonArray([.. older.Select(e => (JsonNode)e.DeepClone()), .. retained.Select(e => e!.DeepClone())]);
        return snapshot;
    }

    public bool Cancel(string runId, string? projectId = null) =>
        tracker.Find(runId) is { } run && !Visible(run.ProjectId, projectId) ? false : tracker.Cancel(runId);

    /// <summary>
    /// Quem pede pode dizer de que projeto é a execução (<paramref name="expectedProjectId"/>); se disser, tem de bater.
    /// Com a central de execuções ligada, o projeto também precisa estar autorizado.
    /// </summary>
    private bool Visible(string runProjectId, string? expectedProjectId) =>
        (expectedProjectId is null || expectedProjectId == runProjectId) && (catalog?.CanAccess(runProjectId) ?? true);

    /// <summary>A central de execuções. Sem catálogo (sessões de teste sem projetos autorizados) ela não existe.</summary>
    public RunCatalog Catalog =>
        catalog ?? throw new EngineException("RUN_CATALOG_UNAVAILABLE", "A central de execuções não está disponível nesta sessão do motor.");
}
