using System.Text.Json.Nodes;
using Mariliz.Application.Ports;
using Mariliz.Core.Text;

namespace Mariliz.Application.Execution;

/// <summary>
/// Desfaz uma aplicação a partir do diário e dos backups. Só restaura um arquivo se ele ainda está exatamente
/// como a aplicação o deixou; uma edição posterior da pessoa nunca é sobrescrita, vira um conflito dito no resultado.
/// </summary>
public sealed class RevertService(IRunStore store, IClock clock)
{
    public Task<JsonObject> ExecuteAsync(RunHandle run, IProjectFiles files, string targetRunId, CancellationToken ct)
    {
        var target = store.LoadJournal(run.ProjectId, targetRunId)
                     ?? throw new EngineException("RUN_NOT_FOUND", "A execução a reverter não foi encontrada.");
        if (target.Kind != RunKinds.Apply) throw new EngineException("NOT_REVERTIBLE", "Só execuções de aplicação podem ser revertidas.");
        if (target.RevertedByRunId is not null) throw new EngineException("ALREADY_REVERTED", "Esta execução já foi revertida.");
        if (!target.ChangesApplied || target.RolledBack) throw new EngineException("NOTHING_TO_REVERT", "Esta execução não deixou alterações para reverter.");

        // Reverter fora de ordem desfaria o estado de execuções posteriores: só a mais recente pode ser revertida.
        var later = store.ListJournals(run.ProjectId)
            .Any(j => j.Kind == RunKinds.Apply && j.StartedAt > target.StartedAt && j.ChangesApplied && !j.RolledBack && j.RevertedByRunId is null);
        if (later) throw new EngineException("NOT_LATEST_RUN", "Há uma aplicação mais recente; reverta primeiro a mais recente.");

        var journal = new RunJournal { RunId = run.RunId, ProjectId = run.ProjectId, Kind = RunKinds.Revert, PlanId = target.PlanId, RevertsRunId = targetRunId, StartedAt = clock.UtcNow };
        var step = new JournalStep { Id = "revert", Title = "Restaurar o estado anterior", Status = "running" };
        journal.Steps.Add(step);
        store.SaveJournal(journal);
        run.SetState(RunStates.Running);
        run.Publish("step.started", new JsonObject { ["id"] = step.Id, ["title"] = step.Title });

        var conflicts = new List<string>();
        var reverted = new List<JournalOp>();

        // Ordem inversa: o que foi aplicado por último é desfeito primeiro.
        foreach (var op in Enumerable.Reverse(target.Ops).Where(o => o.Status is OpStatus.Applied or OpStatus.Conflict))
        {
            // Cancelar aqui deixaria a reversão pela metade; ela só para entre operações e o que sobrar é dito.
            if (ct.IsCancellationRequested) { conflicts.Add($"{op.Path}: reversão interrompida antes deste arquivo."); continue; }

            RevertHelper.Undo(store, run.ProjectId, targetRunId, files, op, conflicts);
            if (op.Status == OpStatus.Reverted) reverted.Add(op);
            run.Publish("file.reverted", new JsonObject { ["path"] = op.Path, ["status"] = op.Status });
        }

        // Conferência pós-reversão: o que foi restaurado precisa bater com o estado anterior.
        var verified = reverted.All(op => op.Op switch
        {
            "create" => !files.FileExists(op.Path),
            "update" or "delete" => files.ReadBytes(op.Path) is { } b && Hashing.Sha256(b) == op.BeforeHash,
            _ => true
        });
        if (!verified) conflicts.Add("A conferência final encontrou arquivos que não voltaram ao estado anterior.");

        target.RevertedByRunId = conflicts.Count == 0 ? run.RunId : null;
        store.SaveJournal(target);

        step.Status = conflicts.Count == 0 ? "completed" : "completed_with_conflicts";
        journal.State = conflicts.Count == 0 ? RunStates.Completed : RunStates.CompletedWithLimitations;
        journal.Pending = conflicts;
        journal.FinishedAt = clock.UtcNow;
        store.SaveJournal(journal);
        run.Publish("step.completed", new JsonObject { ["id"] = step.Id, ["status"] = step.Status });
        return Task.FromResult(ResultJson.BuildRevert(journal, reverted, conflicts, verified));
    }
}
