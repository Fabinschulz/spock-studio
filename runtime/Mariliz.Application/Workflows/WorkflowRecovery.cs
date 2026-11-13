using Mariliz.Application.Execution;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Workflows;

// Uma execução "em andamento" ao subir perdeu o processo que a tocava e não vai terminar sozinha: vira falha. Só running é tocado: awaiting_approval continua retomável e o harness tem recuperação própria. O motor é único por pasta de dados, então nenhuma execução running pertence a outro processo vivo.
public static class WorkflowRecovery
{
    public const string EngineRestarted = "ENGINE_RESTARTED";

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
