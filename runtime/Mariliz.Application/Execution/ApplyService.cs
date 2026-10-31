using System.Text.Json.Nodes;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Execution;

/// <summary>
/// Aplica somente o que foi aprovado. Se algo falha ou a pessoa cancela no meio, tudo o que já foi aplicado é
/// desfeito. Nada é escrito fora do escopo gerenciado.
/// </summary>
public sealed class ApplyService(IProcessRunner runner, IRunStore store, IClock clock, IModelSettings? models = null)
{
    public async Task<JsonObject> ExecuteAsync(
        RunHandle run, string root, IProjectFiles files, PlanSession session, ApplyApprovals approvals,
        Func<CancellationToken, Task<Replanned>> replan, CancellationToken ct,
        Func<CancellationToken, Task<Replanned>>? replanAfterApply = null)
    {
        var journal = new RunJournal
        {
            RunId = run.RunId,
            ProjectId = run.ProjectId,
            Kind = RunKinds.Apply,
            PlanId = session.Plan.PlanId,
            StartedAt = clock.UtcNow
        };
        var execution = new ApplyExecution(
            runner, store, clock, run, journal, root, files, session, approvals, replan, replanAfterApply ?? replan, ct, models);

        try
        {
            return await execution.RunAsync();
        }
        catch (OperationCanceledException)
        {
            return Rollback(run, files, journal, execution.Applied, RunStates.Cancelled,
                "Execução cancelada; as alterações já feitas foram desfeitas.", null);
        }
        catch (EngineException ex)
        {
            return Rollback(run, files, journal, execution.Applied, RunStates.Failed, ex.Message, ex.Code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Rollback(run, files, journal, execution.Applied, RunStates.Failed,
                $"Falha ao aplicar ({ex.GetType().Name}); as alterações foram desfeitas.", "APPLY_FAILED");
        }
    }

    private JsonObject Rollback(
        RunHandle run, IProjectFiles files, RunJournal journal, IReadOnlyList<JournalOp> applied, string state, string message, string? code)
    {
        var conflicts = new List<string>();

        foreach (var op in applied.Reverse())
        {
            try
            {
                OperationUndoer.Undo(store, run.ProjectId, run.RunId, files, op, conflicts);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                conflicts.Add($"{op.Path}: não foi possível desfazer ({ex.GetType().Name}).");
            }
        }

        journal.RolledBack = applied.Count > 0 && conflicts.Count == 0;
        journal.ChangesApplied = applied.Count > 0 && conflicts.Count > 0;
        journal.State = conflicts.Count > 0 ? RunStates.RequiresIntervention : state;
        journal.Error = code is null ? message : $"{code}: {message}";
        journal.FinishedAt = clock.UtcNow;
        journal.Pending = conflicts;
        store.SaveJournal(journal);

        return ResultJson.BuildFailure(journal, message, code, conflicts);
    }
}
