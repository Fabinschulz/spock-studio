using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>
/// Consulta e cancelamento de qualquer execução (aplicação, validação, reversão ou workflow), independentes da jornada
/// que a iniciou.
/// </summary>
public sealed class RunQueries(RunTracker tracker, IRunStore store)
{
    /// <summary>
    /// Com <paramref name="sinceSeq"/>, devolve todos os eventos depois desse número: os que a memória já descartou vêm do
    /// histórico em disco, para a interface recuperar o que perdeu numa reconexão.
    /// </summary>
    public JsonObject Snapshot(string runId, int? sinceSeq = null)
    {
        var run = tracker.Find(runId) ?? throw new EngineException("RUN_NOT_FOUND", "Execução não encontrada.");
        var snapshot = run.Snapshot(sinceSeq);
        if (sinceSeq is not { } since || snapshot["firstSeq"]!.GetValue<int>() <= since + 1) return snapshot;

        var retained = snapshot["events"]!.AsArray();
        var firstRetained = snapshot["firstSeq"]!.GetValue<int>();
        var older = store.LoadEvents(run.ProjectId, run.RunId)
            .Where(e => e["seq"] is JsonValue seq && seq.GetValue<int>() > since && seq.GetValue<int>() < firstRetained);
        snapshot["events"] = new JsonArray([.. older.Select(e => (JsonNode)e.DeepClone()), .. retained.Select(e => e!.DeepClone())]);
        return snapshot;
    }

    public bool Cancel(string runId) => tracker.Cancel(runId);
}
