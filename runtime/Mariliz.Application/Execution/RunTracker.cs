using System.Text.Json.Nodes;

namespace Mariliz.Application.Execution;

public sealed class RunTracker
{
    private readonly Dictionary<string, RunHandle> _runs = new();
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
            _runs[runId] = handle;
            _activeByProject[projectId] = runId;
            return true;
        }
    }

    public RunHandle? Find(string runId)
    {
        lock (_gate) return _runs.GetValueOrDefault(runId);
    }

    public bool Cancel(string runId)
    {
        var run = Find(runId);
        if (run is null || RunStates.IsTerminal(run.State)) return false;
        run.Cancellation.Cancel();
        return true;
    }

    public async Task WaitForAllAsync()
    {
        List<Task> tasks;
        lock (_gate) tasks = _runs.Values.Select(r => r.Completion).Where(t => t is not null).Cast<Task>().ToList();
        await Task.WhenAll(tasks);
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
}
