using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;

namespace Mariliz.Application.Security;

// Achar é só leitura. Corrigir segue o caminho do Harness: plano com diferença, aprovação pelo id do plano, backup, diário,
// reversão possível e uma nova varredura que diz o que ainda falta.
public sealed class SecurityService(
    WorkspaceAuthorizer workspace,
    IProjectFilesFactory filesFactory,
    IRunStore store,
    IClock clock,
    RunTracker tracker)
{
    public SecurityReport Scan(string projectId)
    {
        var (findings, scanned, truncated) = SecurityScanner.Scan(Open(projectId));
        return new SecurityReport(projectId, clock.UtcNow, scanned, truncated, findings);
    }

    public SecurityPlan Plan(string projectId)
    {
        var files = Open(projectId);
        return SecurityFixPlanner.Plan(projectId, files, SecurityScanner.Scan(files).Findings).Plan;
    }

    public RunHandle StartFix(string projectId, string planId)
    {
        var files = Open(projectId);
        var before = SecurityScanner.Scan(files).Findings;
        var (plan, fixes) = SecurityFixPlanner.Plan(projectId, files, before);
        if (fixes.Count == 0) throw new EngineException("NOTHING_TO_APPLY", "Não há o que corrigir automaticamente.");
        if (plan.PlanId != planId)
            throw new EngineException("STALE_PLAN", "O projeto mudou desde a revisão. Revise as mudanças de novo antes de aprovar.");
        if (!tracker.TryStart(projectId, RunKinds.Security, out var run) || run is null) throw EngineErrors.RunInProgress();

        run.Start(() =>
        {
            var result = Execute(run, files, plan, fixes);
            run.Complete(result["outcome"]!.GetValue<string>(), result);
        });
        return run;
    }

    private JsonObject Execute(RunHandle run, IProjectFiles files, SecurityPlan plan, IReadOnlyList<PlannedFix> fixes)
    {
        var journal = new RunJournal
        {
            RunId = run.RunId, ProjectId = run.ProjectId, Kind = RunKinds.Security, PlanId = plan.PlanId, StartedAt = clock.UtcNow
        };
        var recorder = new RunJournalRecorder(run, journal, store);
        run.SetState(RunStates.Running);

        var backup = recorder.BeginStep("backup", "Guardar uma cópia do que vai mudar");
        foreach (var fix in fixes)
            if (files.ReadBytes(fix.Change.Path) is { } bytes) store.SaveBackup(run.ProjectId, run.RunId, fix.Change.Path, bytes);
        recorder.CompleteStep(backup);

        var write = recorder.BeginStep("write", "Aplicar a correção aprovada");
        try
        {
            foreach (var fix in fixes) Write(run, files, journal, recorder, fix);
            recorder.CompleteStep(write, StepStatus.Completed, $"{fixes.Count} arquivo(s)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EngineException)
        {
            RollBack(run, files, journal);
            recorder.CompleteStep(write, StepStatus.Failed, "A gravação falhou e o que já tinha mudado foi desfeito.");
            return Finish(journal, recorder, RunStates.Failed, Result(run, journal, plan, null, ex.Message));
        }

        var validate = recorder.BeginStep("validate", "Varrer o projeto de novo");
        var after = SecurityScanner.Scan(files).Findings;
        recorder.CompleteStep(validate, StepStatus.Completed, $"{after.Count} achado(s) restante(s)");
        return Finish(journal, recorder, RunStates.Completed, Result(run, journal, plan, after, null));
    }

    private static void Write(RunHandle run, IProjectFiles files, RunJournal journal, RunJournalRecorder recorder, PlannedFix fix)
    {
        var op = new JournalOp
        {
            Path = fix.Change.Path,
            Op = fix.Change.Op,
            Source = OpSources.Security,
            BeforeHash = fix.Change.BeforeHash,
            AfterHash = fix.Change.AfterHash,
            BackedUp = fix.Change.BeforeHash is not null
        };
        journal.Ops.Add(op);
        recorder.Save();

        files.WriteAtomic(fix.Change.Path, Encoding.UTF8.GetBytes(fix.Content), WriteScope.SecurityFix);
        if (Hashing.Sha256(files.ReadBytes(fix.Change.Path) ?? []) != fix.Change.AfterHash)
            throw new EngineException("WRITE_VERIFICATION_FAILED", $"O arquivo '{fix.Change.Path}' foi gravado, mas não confere com o plano.");

        op.Status = OpStatus.Applied;
        journal.ChangesApplied = true;
        recorder.Save();
        run.Publish("file.applied", new JsonObject { ["path"] = fix.Change.Path, ["op"] = fix.Change.Op });
    }

    private void RollBack(RunHandle run, IProjectFiles files, RunJournal journal)
    {
        var conflicts = new List<string>();
        foreach (var op in Enumerable.Reverse(journal.Ops).Where(op => op.Status == OpStatus.Applied))
            OperationUndoer.Undo(store, run.ProjectId, run.RunId, files, op, conflicts);
        journal.RolledBack = true;
        journal.Pending = conflicts;
    }

    private JsonObject Finish(RunJournal journal, RunJournalRecorder recorder, string state, JsonObject result)
    {
        journal.State = state;
        journal.FinishedAt = clock.UtcNow;
        recorder.Save();
        result["outcome"] = state;
        return result;
    }

    private static JsonObject Result(RunHandle run, RunJournal journal, SecurityPlan plan, IReadOnlyList<SecurityFinding>? after, string? error)
    {
        var remaining = after?.Select(finding => finding.Id).ToHashSet(StringComparer.Ordinal);
        return new JsonObject
        {
            ["runId"] = run.RunId,
            ["kind"] = RunKinds.Security,
            ["changes"] = new JsonArray(journal.Ops.Select(op => (JsonNode)new JsonObject { ["path"] = op.Path, ["op"] = op.Op, ["status"] = op.Status }).ToArray()),
            ["fixed"] = remaining is null ? 0 : plan.FindingIds.Count(id => !remaining.Contains(id)),
            ["remaining"] = after?.Count,
            ["error"] = error
        };
    }

    private IProjectFiles Open(string projectId) =>
        workspace.TryGetRoot(projectId, out var root) ? filesFactory.Open(root) : throw EngineErrors.ProjectNotAuthorized();
}
