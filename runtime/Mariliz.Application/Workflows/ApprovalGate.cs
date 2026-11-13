using System.Collections.Concurrent;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Workflows;

// A espera não tem prazo; só o cancelamento da execução a interrompe.
internal sealed class ApprovalGate
{
    private sealed record Pending(TaskCompletionSource<IReadOnlyList<string>> Gate, RunJournal Journal);

    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    // O registro vem antes do anúncio: um workflow.resume que chegue logo depois do evento já encontra a aprovação.
    public async Task<IReadOnlyList<string>> WaitAsync(string runId, RunJournal journal, CancellationToken cancellation, Action announce)
    {
        var gate = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[runId] = new Pending(gate, journal);
        try
        {
            announce();
            using (cancellation.Register(() => gate.TrySetCanceled(cancellation)))
                return await gate.Task;
        }
        finally
        {
            _pending.TryRemove(runId, out _);
        }
    }

    // verify pode lançar. Falso quando não há execução aguardando.
    public bool TryRelease(string runId, Action<RunJournal> verify, IReadOnlyList<string> approvedActions)
    {
        if (!_pending.TryGetValue(runId, out var pending)) return false;
        verify(pending.Journal);
        return pending.Gate.TrySetResult(approvedActions);
    }

    public void Discard(string runId) => _pending.TryRemove(runId, out _);
}
