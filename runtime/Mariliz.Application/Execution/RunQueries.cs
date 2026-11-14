using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

public sealed class RunQueries(RunTracker tracker, IRunStore store, RunCatalog catalog)
{
    // Os eventos que a memória já descartou vêm do histórico em disco, para a interface recuperar o que perdeu numa reconexão.
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

    // O projeto também precisa estar autorizado: a regra não é opcional.
    private bool Visible(string runProjectId, string? expectedProjectId) =>
        (expectedProjectId is null || expectedProjectId == runProjectId) && catalog.CanAccess(runProjectId);

    public RunCatalog Catalog => catalog;
}
