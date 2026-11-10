using System.Collections.Concurrent;
using Mariliz.Application.Execution;

namespace Mariliz.Application.Workflows;

/// <summary>
/// A espera pela aprovação humana: execuções pausadas, e a decisão que as libera. A espera não tem prazo; só o
/// cancelamento da execução a interrompe.
/// </summary>
internal sealed class ApprovalGate
{
    private sealed record Pending(TaskCompletionSource<IReadOnlyList<string>> Gate, RunJournal Journal);

    private readonly ConcurrentDictionary<string, Pending> _pending = new();

    /// <summary>
    /// Registra a espera, chama <paramref name="announce"/> e aguarda a decisão. O registro vem antes do anúncio: um
    /// <c>workflow.resume</c> que chegue logo depois do evento já encontra a aprovação.
    /// </summary>
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

    /// <summary>
    /// Libera a espera com a decisão, depois de <paramref name="verify"/> conferi-la contra o diário da aprovação aberta
    /// (pode lançar). Falso quando não há execução aguardando.
    /// </summary>
    public bool TryRelease(string runId, Action<RunJournal> verify, IReadOnlyList<string> approvedActions)
    {
        if (!_pending.TryGetValue(runId, out var pending)) return false;
        verify(pending.Journal);
        return pending.Gate.TrySetResult(approvedActions);
    }

    public void Discard(string runId) => _pending.TryRemove(runId, out _);
}
