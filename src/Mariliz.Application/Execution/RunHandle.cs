using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>Uma execução em andamento ou concluída, com eventos e cancelamento.</summary>
public sealed class RunHandle
{
    private readonly object _gate = new();
    private readonly List<JsonObject> _events = [];
    private int _seq;

    public RunHandle(string runId, string projectId, string kind, Action<string, int, string, JsonObject> emit)
    {
        RunId = runId;
        ProjectId = projectId;
        Kind = kind;
        Emit = emit;
    }

    public string RunId { get; }
    public string ProjectId { get; }
    public string Kind { get; }
    public string State { get; private set; } = RunStates.Running;
    public JsonObject? Result { get; private set; }
    public CancellationTokenSource Cancellation { get; } = new();
    public Task? Completion { get; set; }
    private Action<string, int, string, JsonObject> Emit { get; }

    public void Publish(string type, JsonObject data)
    {
        int seq;
        lock (_gate)
        {
            seq = _seq++;
            _events.Add(new JsonObject { ["seq"] = seq, ["type"] = type, ["data"] = data.DeepClone() });
            if (_events.Count > 500) _events.RemoveAt(0);
        }
        Emit(RunId, seq, type, data);
    }

    public void SetState(string state, string? detail = null)
    {
        lock (_gate) State = state;
        Publish("run.state", new JsonObject { ["state"] = state, ["detail"] = detail });
    }

    public void Complete(string state, JsonObject result)
    {
        lock (_gate) { State = state; Result = result; }
        Publish("run.completed", new JsonObject { ["state"] = state });
    }

    public JsonObject Snapshot()
    {
        lock (_gate)
        {
            return new JsonObject
            {
                ["runId"] = RunId,
                ["projectId"] = ProjectId,
                ["kind"] = Kind,
                ["state"] = State,
                ["lastSeq"] = _seq - 1,
                ["events"] = new JsonArray(_events.TakeLast(100).Select(e => (JsonNode)e.DeepClone()).ToArray()),
                ["result"] = Result?.DeepClone()
            };
        }
    }
}
