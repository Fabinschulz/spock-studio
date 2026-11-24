using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

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
        var (plan, writes) = SecurityFixPlanner.Plan(projectId, files, before);
        if (writes.Count == 0) throw new EngineException("NOTHING_TO_APPLY", "Não há o que corrigir automaticamente.");
        if (plan.PlanId != planId)
            throw EngineErrors.StalePlan("O projeto mudou desde a revisão. Revise as mudanças de novo antes de aprovar.");

        var run = new ReviewedWriteRun(tracker, store, clock);
        return run.Start(projectId, RunKinds.Security, plan.PlanId, files, WriteScope.SecurityFix, OpSources.Security, writes,
            (handle, journal, recorder, error) =>
            {
                if (error is not null)
                {
                    run.Finish(journal, recorder, RunStates.Failed);
                    return Result(handle, journal, plan, null, error);
                }

                var validate = recorder.BeginStep("validate", "Varrer o projeto de novo");
                var after = SecurityScanner.Scan(files).Findings;
                recorder.CompleteStep(validate, StepStatus.Completed, $"{after.Count} achado(s) restante(s)");
                run.Finish(journal, recorder, RunStates.Completed);
                return Result(handle, journal, plan, after, null);
            });
    }

    private static JsonObject Result(RunHandle run, RunJournal journal, SecurityPlan plan, IReadOnlyList<SecurityFinding>? after, string? error)
    {
        var remaining = after?.Select(finding => finding.Id).ToHashSet(StringComparer.Ordinal);
        return new JsonObject
        {
            ["runId"] = run.RunId,
            ["kind"] = RunKinds.Security,
            ["changes"] = ReviewedWriteRun.Changes(journal),
            ["fixed"] = remaining is null ? 0 : plan.FindingIds.Count(id => !remaining.Contains(id)),
            ["remaining"] = after?.Count,
            ["error"] = error,
            ["outcome"] = journal.State
        };
    }

    private IProjectFiles Open(string projectId) =>
        filesFactory.Open(workspace.RequireRoot(projectId));
}
