using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Validation;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>
/// A jornada "Configurar projeto" como casos de uso: descobrir, planejar, aplicar, validar e reverter.
/// UI e CLI chamam esta mesma classe; nenhuma regra mora na interface.
/// </summary>
public sealed class ConfigureProjectService(
    WorkspaceAuthorizer workspace,
    IProjectFilesFactory filesFactory,
    IProcessRunner runner,
    IRunStore store,
    IClock clock,
    RunTracker tracker,
    IModelSettings? models = null)
{
    private readonly PlanSessionStore _plans = new();
    private readonly ProjectPlanner _planner = new(runner);
    private readonly ApplyService _apply = new(runner, store, clock, models);
    private readonly RevertService _revert = new(store, clock);

    public async Task<DiscoveryReport> DiscoverAsync(string projectId, CancellationToken ct)
    {
        var (root, files) = Open(projectId);
        return await _planner.DiscoverAsync(projectId, root, files, ct);
    }

    public async Task<ConfigurationPlan> PlanAsync(string projectId, PlanOptions options, CancellationToken ct)
    {
        var (root, files) = Open(projectId);
        var replanned = await _planner.ReplanAsync(projectId, root, files, options, ct);
        _plans.Save(new PlanSession(projectId, root, options, replanned.Plan, replanned.Composed));
        return replanned.Plan;
    }

    public IReadOnlyList<RunSummary> ListRuns(string projectId)
    {
        Open(projectId);
        return store.ListJournals(projectId)
            .OrderByDescending(j => j.StartedAt)
            .Select(j => new RunSummary(j.RunId, j.Kind, j.State, j.StartedAt, j.ChangesApplied && !j.RolledBack, j.RevertedByRunId is not null))
            .ToList();
    }

    public RunHandle StartApply(string projectId, string planId, ApplyApprovals approvals)
    {
        var (root, files) = Open(projectId);

        var session = _plans.Find(planId);
        if (session is null || session.ProjectId != projectId)
            throw new EngineException("PLAN_UNKNOWN", "O plano não está mais disponível. Gere o plano de novo.");
        if (HasNothingToApply(session.Plan))
            throw new EngineException("NOTHING_TO_APPLY", "O plano não tem o que aplicar.");

        var run = Start(projectId, RunKinds.Apply);
        Func<CancellationToken, Task<Replanned>> ReplanWith(PlanOptions options) =>
            ct => _planner.ReplanAsync(projectId, root, files, options, ct);

        run.Start(async () =>
        {
            // O pedido de atualizar a estrutura de especificação vale uma vez: depois de aplicado, a idempotência é medida sem ele.
            var result = await _apply.ExecuteAsync(run, root, files, session, approvals,
                ReplanWith(session.Options), run.Cancellation.Token, ReplanWith(session.Options with { UpdateSpecification = false }));
            run.Complete(OutcomeOf(result), result);
        });
        return run;
    }

    public RunHandle StartValidate(string projectId)
    {
        var (root, files) = Open(projectId);
        var run = Start(projectId, RunKinds.Validate);

        var startedAt = clock.UtcNow;

        run.Start(async () =>
        {
            try
            {
                run.SetState(RunStates.Validating);
                var report = await ValidationService.RunAsync(runner, store, files, root, projectId,
                    ct => _planner.ReplanAsync(projectId, root, files, new PlanOptions(null), ct), run.Cancellation.Token, models);

                var result = ResultJson.BuildValidation(run.RunId, projectId, report);
                SaveValidationJournal(run, startedAt, report, result);
                run.Complete(OutcomeOf(result), result);
            }
            catch (OperationCanceledException)
            {
                run.Complete(RunStates.Cancelled, new JsonObject { ["runId"] = run.RunId, ["outcome"] = RunStates.Cancelled });
            }
        });
        return run;
    }

    public RunHandle StartRevert(string projectId, string targetRunId)
    {
        var (_, files) = Open(projectId);
        var run = Start(projectId, RunKinds.Revert);

        run.Start(() =>
        {
            try
            {
                var result = _revert.Execute(run, files, targetRunId, run.Cancellation.Token);
                run.Complete(OutcomeOf(result), result);
            }
            catch (EngineException ex)
            {
                run.Complete(RunStates.Failed, new JsonObject
                {
                    ["runId"] = run.RunId,
                    ["outcome"] = RunStates.Failed,
                    ["kind"] = RunKinds.Revert,
                    ["error"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message }
                });
            }
        });
        return run;
    }

    /// <summary>Sem mudanças nem comandos e sem ambiente escolhido: aplicar não faria nada. Com ambiente, aplica para revalidar.</summary>
    private static bool HasNothingToApply(ConfigurationPlan plan) => plan.IsEmpty && plan.AiEnvironment.Length == 0;

    private static string OutcomeOf(JsonObject result) => result["outcome"]?.GetValue<string>() ?? RunStates.Failed;

    private void SaveValidationJournal(RunHandle run, DateTimeOffset startedAt, Core.Evals.ValidationReport report, JsonObject result)
    {
        store.SaveJournal(new RunJournal
        {
            RunId = run.RunId,
            ProjectId = run.ProjectId,
            Kind = RunKinds.Validate,
            StartedAt = startedAt,
            FinishedAt = clock.UtcNow,
            State = OutcomeOf(result),
            Evals = EvalSummary.From(report)
        });
    }

    private RunHandle Start(string projectId, string kind)
    {
        if (!tracker.TryStart(projectId, kind, out var run) || run is null)
            throw EngineErrors.RunInProgress();
        return run;
    }

    private (string Root, IProjectFiles Files) Open(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root))
            throw EngineErrors.ProjectNotAuthorized();
        return (root, filesFactory.Open(root));
    }
}
