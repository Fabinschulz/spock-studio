using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

/// <summary>
/// Desfaz uma aplicação a partir do diário e dos backups. Uma edição posterior da pessoa nunca é sobrescrita:
/// vira um conflito dito no resultado.
/// </summary>
public sealed class RevertService(IRunStore store, IClock clock)
{
    public JsonObject Execute(RunHandle run, IProjectFiles files, string targetRunId, CancellationToken ct)
    {
        var target = LoadRevertibleTarget(run.ProjectId, targetRunId);

        var journal = new RunJournal
        {
            RunId = run.RunId,
            ProjectId = run.ProjectId,
            Kind = RunKinds.Revert,
            PlanId = target.PlanId,
            RevertsRunId = targetRunId,
            StartedAt = clock.UtcNow
        };
        var recorder = new RunJournalRecorder(run, journal, store);

        run.SetState(RunStates.Running);
        var step = recorder.BeginStep("revert", "Restaurar o estado anterior");

        var conflicts = new List<string>();
        var reverted = UndoOperations(run, files, target, conflicts, ct);

        var verified = reverted.All(op => IsRestored(files, op));
        if (!verified) conflicts.Add("A conferência final encontrou arquivos que não voltaram ao estado anterior.");

        var clean = conflicts.Count == 0;
        target.RevertedByRunId = clean ? run.RunId : null;
        store.SaveJournal(target);

        journal.State = clean ? RunStates.Completed : RunStates.CompletedWithLimitations;
        journal.Pending = conflicts;
        journal.FinishedAt = clock.UtcNow;
        recorder.CompleteStep(step, clean ? StepStatus.Completed : StepStatus.CompletedWithConflicts);

        return ResultJson.BuildRevert(journal, reverted, conflicts, verified);
    }

    private RunJournal LoadRevertibleTarget(string projectId, string targetRunId)
    {
        var target = store.LoadJournal(projectId, targetRunId)
                     ?? throw new EngineException("RUN_NOT_FOUND", "A execução a reverter não foi encontrada.");

        if (target.Kind != RunKinds.Apply)
            throw new EngineException("NOT_REVERTIBLE", "Só execuções de aplicação podem ser revertidas.");
        if (target.RevertedByRunId is not null)
            throw new EngineException("ALREADY_REVERTED", "Esta execução já foi revertida.");
        if (!target.ChangesApplied || target.RolledBack)
            throw new EngineException("NOTHING_TO_REVERT", "Esta execução não deixou alterações para reverter.");

        var hasLaterApply = store.ListJournals(projectId)
            .Any(j => j.Kind == RunKinds.Apply && j.StartedAt > target.StartedAt && j.CanBeReverted);
        if (hasLaterApply)
            throw new EngineException("NOT_LATEST_RUN", "Há uma aplicação mais recente; reverta primeiro a mais recente.");

        return target;
    }

    private List<JournalOp> UndoOperations(RunHandle run, IProjectFiles files, RunJournal target, List<string> conflicts, CancellationToken ct)
    {
        var reverted = new List<JournalOp>();

        foreach (var op in Enumerable.Reverse(target.Ops).Where(o => o.Status is OpStatus.Applied or OpStatus.Conflict))
        {
            // Cancelar no meio deixaria a reversão pela metade: ela só para entre operações e o que sobrar é dito.
            if (ct.IsCancellationRequested)
            {
                conflicts.Add($"{op.Path}: reversão interrompida antes deste arquivo.");
                continue;
            }

            OperationUndoer.Undo(store, run.ProjectId, target.RunId, files, op, conflicts);
            if (op.Status == OpStatus.Reverted) reverted.Add(op);
            run.Publish("file.reverted", new JsonObject { ["path"] = op.Path, ["status"] = op.Status });
        }

        return reverted;
    }

    private static bool IsRestored(IProjectFiles files, JournalOp op) => op.Op switch
    {
        OpKinds.Create => !files.FileExists(op.Path),
        OpKinds.Update or OpKinds.Delete => files.ReadBytes(op.Path) is { } bytes && Hashing.Sha256(bytes) == op.BeforeHash,
        _ => true
    };
}
