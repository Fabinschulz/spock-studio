using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>Uma execução em andamento ou concluída, com eventos e cancelamento.</summary>
public sealed class RunHandle
{
    private const int Retained = 500;
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

    /// <summary>Recebe cada evento publicado (<c>{seq, type, data}</c>), na ordem, para o histórico em disco.</summary>
    public Action<JsonObject>? Recorder { get; set; }

    private Action<string, int, string, JsonObject> Emit { get; }

    /// <summary>
    /// Roda o corpo em segundo plano e garante que a execução termina. Os serviços tratam os erros que conhecem; uma exceção
    /// que escapar vira <c>failed</c> com o tipo (nunca a mensagem, que pode ter caminhos), em vez de uma execução presa
    /// que impede o projeto de começar outra.
    /// </summary>
    public void Start(Func<Task> body)
    {
        Completion = Task.Run(async () =>
        {
            try
            {
                await body();
            }
            catch (Exception ex) when (!RunStates.IsTerminal(State))
            {
                Complete(RunStates.Failed, new JsonObject
                {
                    ["runId"] = RunId,
                    ["kind"] = Kind,
                    ["outcome"] = RunStates.Failed,
                    ["error"] = new JsonObject { ["code"] = "INTERNAL_ERROR", ["message"] = $"Falha interna ({ex.GetType().Name})." }
                });
            }
        });
    }

    public void Start(Action body) => Start(() =>
    {
        body();
        return Task.CompletedTask;
    });

    /// <summary>Uma execução religada depois de o motor cair continua a numeração que já foi publicada.</summary>
    public void ContinueFrom(int nextSeq)
    {
        lock (_gate) _seq = Math.Max(_seq, nextSeq);
    }

    public int Publish(string type, JsonObject data)
    {
        int seq;
        JsonObject recorded;
        lock (_gate)
        {
            seq = _seq++;
            recorded = new JsonObject { ["seq"] = seq, ["type"] = type, ["data"] = data.DeepClone() };
            _events.Add(recorded);
            if (_events.Count > Retained) _events.RemoveAt(0);
            Recorder?.Invoke((JsonObject)recorded.DeepClone());
        }
        Emit(RunId, seq, type, data);
        return seq;
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

    /// <summary>
    /// Sem <paramref name="sinceSeq"/>, os 100 eventos mais recentes. Com ele, todos os retidos depois desse número;
    /// <c>firstSeq</c> diz onde a memória começa, para quem precisar buscar o resto no histórico.
    /// </summary>
    public JsonObject Snapshot(int? sinceSeq = null)
    {
        lock (_gate)
        {
            var events = sinceSeq is { } since
                ? _events.Where(e => e["seq"]!.GetValue<int>() > since)
                : _events.TakeLast(100);
            return new JsonObject
            {
                ["runId"] = RunId,
                ["projectId"] = ProjectId,
                ["kind"] = Kind,
                ["state"] = State,
                ["lastSeq"] = _seq - 1,
                ["firstSeq"] = _events.Count == 0 ? _seq : _events[0]["seq"]!.GetValue<int>(),
                ["events"] = new JsonArray(events.Select(e => (JsonNode)e.DeepClone()).ToArray()),
                ["result"] = Result?.DeepClone()
            };
        }
    }
}
