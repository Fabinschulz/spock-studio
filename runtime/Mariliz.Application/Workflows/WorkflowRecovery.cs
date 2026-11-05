using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Roda uma vez, quando o motor sobe. Uma execução de workflow que o diário ainda chama de "em andamento" perdeu o processo que
/// a tocava (o motor caiu, foi encerrado à força ou o computador desligou): ela não vai terminar sozinha, e deixá-la assim
/// a mostraria para sempre como rodando. Vira falha, com o motivo registrado.
/// </summary>
/// <remarks>
/// Só o estado <c>running</c> é tocado. Uma execução que espera aprovação (<c>awaiting_approval</c>) continua retomável a
/// partir do diário e do checkpoint, e as da aplicação do harness têm recuperação própria. O motor é único por pasta de dados
/// (o host só abre uma instância), então nenhuma execução <c>running</c> pertence a outro processo vivo.
/// </remarks>
public static class WorkflowRecovery
{
    public const string EngineRestarted = "ENGINE_RESTARTED";

    /// <summary>Marca as execuções interrompidas como falhas e devolve quantas eram.</summary>
    public static int MarkInterrupted(IRunStore store, IClock clock)
    {
        var count = 0;
        foreach (var projectId in store.ListProjectIds())
        {
            foreach (var journal in store.ListJournals(projectId))
            {
                if (journal.Kind != WorkflowService.Kind || journal.State != RunStates.Running) continue;

                ActionGovernance.SkipPending(journal);
                journal.State = RunStates.Failed;
                journal.FinishedAt = clock.UtcNow;
                journal.Error = EngineRestarted;
                store.SaveJournal(journal);
                count++;
            }
        }
        return count;
    }
}
