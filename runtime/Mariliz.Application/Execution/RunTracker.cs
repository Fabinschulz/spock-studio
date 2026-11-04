using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

/// <summary>
/// Execuções do processo. As terminadas ficam em memória para o retrato e a interface; além de
/// <see cref="RetainedFinished"/>, as mais antigas saem (o diário e os eventos continuam em disco).
/// </summary>
public sealed class RunTracker
{
    internal const int RetainedFinished = 100;

    private readonly Dictionary<string, RunHandle> _runs = new();
    private readonly Queue<string> _order = new();
    private readonly Dictionary<string, string> _activeByProject = new();
    private readonly object _gate = new();
    private readonly Action<string, int, string, JsonObject> _emit;

    public RunTracker(Action<string, int, string, JsonObject>? emit = null) => _emit = emit ?? ((_, _, _, _) => { });

    /// <summary>Uma execução por projeto de cada vez: evita escritas concorrentes na mesma pasta.</summary>
    public bool TryStart(string projectId, string kind, out RunHandle? handle)
    {
        lock (_gate)
        {
            if (_activeByProject.TryGetValue(projectId, out var active) && _runs.TryGetValue(active, out var current) && !RunStates.IsTerminal(current.State))
            {
                handle = null;
                return false;
            }
            var runId = "run-" + Guid.NewGuid().ToString("N")[..12];
            handle = new RunHandle(runId, projectId, kind, _emit);
            Track(handle);
            return true;
        }
    }

    public RunHandle? Find(string runId)
    {
        lock (_gate) return _runs.GetValueOrDefault(runId);
    }

    /// <summary>Religa uma execução que só existe no diário, com o mesmo id, depois que o processo caiu.</summary>
    public RunHandle? TryRehydrate(string runId, string projectId, string kind)
    {
        lock (_gate)
        {
            if (_runs.ContainsKey(runId)) return null;
            if (_activeByProject.TryGetValue(projectId, out var active) && _runs.TryGetValue(active, out var current) && !RunStates.IsTerminal(current.State))
                return null;
            var handle = new RunHandle(runId, projectId, kind, _emit);
            Track(handle);
            return handle;
        }
    }

    public bool Cancel(string runId)
    {
        var run = Find(runId);
        if (run is null || RunStates.IsTerminal(run.State)) return false;
        run.Cancellation.Cancel();
        return true;
    }

    /// <summary>Ao encerrar, pede o cancelamento de tudo o que está rodando e espera o desfazer terminar.</summary>
    public async Task CancelAllAndWaitAsync(TimeSpan timeout)
    {
        List<RunHandle> running;
        lock (_gate) running = _runs.Values.Where(r => !RunStates.IsTerminal(r.State)).ToList();
        foreach (var run in running) run.Cancellation.Cancel();
        var tasks = running.Select(r => r.Completion).Where(t => t is not null).Cast<Task>().ToArray();
        if (tasks.Length == 0) return;
        await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(timeout));
    }

    private void Track(RunHandle handle)
    {
        _runs[handle.RunId] = handle;
        _activeByProject[handle.ProjectId] = handle.RunId;
        _order.Enqueue(handle.RunId);
        Prune();
    }

    /// <summary>Descarta as terminadas mais antigas além do limite; uma execução em andamento nunca sai.</summary>
    private void Prune()
    {
        var finished = _runs.Values.Count(run => RunStates.IsTerminal(run.State));
        var pending = _order.Count;
        while (finished > RetainedFinished && pending-- > 0)
        {
            var runId = _order.Dequeue();
            if (!_runs.TryGetValue(runId, out var run)) continue;
            if (!RunStates.IsTerminal(run.State))
            {
                _order.Enqueue(runId);
                continue;
            }
            _runs.Remove(runId);
            if (_activeByProject.TryGetValue(run.ProjectId, out var active) && active == runId) _activeByProject.Remove(run.ProjectId);
            finished--;
        }
    }
}
