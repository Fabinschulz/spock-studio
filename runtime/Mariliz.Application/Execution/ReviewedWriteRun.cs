using System.Text.Json.Nodes;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Execution;

// O ciclo "diff aprovado, execução reversível": diário com cópia antes, conferência por hash e desfazer pela tela de
// execuções. Infraestrutura, bootstrap do state, documentos da ideia e correção de segurança gravam por aqui.
internal sealed class ReviewedWriteRun(RunTracker tracker, IRunStore store, IClock clock)
{
    /// <param name="recorded">Chamado com o diário já salvo, antes de a execução terminar (a trilha de auditoria).</param>
    public RunHandle Start(string projectId, string kind, string planId, IProjectFiles files, WriteScope scope, string source,
        IReadOnlyList<ApprovedWrite> writes, Action<RunHandle, RunJournal>? recorded = null) =>
        Start(projectId, kind, planId, files, scope, source, writes, (run, journal, recorder, error) =>
        {
            Finish(journal, recorder, error is null ? RunStates.Completed : RunStates.Failed);
            recorded?.Invoke(run, journal);
            return new JsonObject
            {
                ["runId"] = run.RunId,
                ["kind"] = kind,
                ["outcome"] = journal.State,
                ["files"] = Changes(journal),
                ["error"] = error
            };
        });

    /// <param name="finish">Para quem confere o projeto depois de gravar e devolve outro resultado. Ele chama <see cref="Finish"/>.</param>
    public RunHandle Start(string projectId, string kind, string planId, IProjectFiles files, WriteScope scope, string source,
        IReadOnlyList<ApprovedWrite> writes, Func<RunHandle, RunJournal, RunJournalRecorder, string?, JsonObject> finish)
    {
        if (!tracker.TryStart(projectId, kind, out var run) || run is null) throw EngineErrors.RunInProgress();

        run.Start(() =>
        {
            var journal = new RunJournal { RunId = run.RunId, ProjectId = projectId, Kind = kind, PlanId = planId, StartedAt = clock.UtcNow };
            var recorder = new RunJournalRecorder(run, journal, store);
            run.SetState(RunStates.Running);
            var error = new ApprovedWriter(store, files, scope, source).Apply(run, journal, recorder, writes);
            var result = finish(run, journal, recorder, error);
            run.Complete(journal.State, result);
        });
        return run;
    }

    public void Finish(RunJournal journal, RunJournalRecorder recorder, string state)
    {
        journal.State = state;
        journal.FinishedAt = clock.UtcNow;
        recorder.Save();
    }

    public static JsonArray Changes(RunJournal journal) =>
        new(journal.Ops.Select(op => (JsonNode)new JsonObject { ["path"] = op.Path, ["op"] = op.Op, ["status"] = op.Status }).ToArray());
}
