using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Validation;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

public sealed record RunSummary(string RunId, string Kind, string State, DateTimeOffset StartedAt, bool ChangesApplied, bool Reverted);

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
    RunTracker tracker)
{
    private readonly Dictionary<string, PlanSession> _plans = new();
    private readonly object _plansGate = new();
    private readonly ProjectDiscoverer _discoverer = new(runner);
    private readonly ApplyService _apply = new(runner, store, clock);
    private readonly RevertService _revert = new(store, clock);

    // ------------------------------------------------------------------ Leitura

    public async Task<DiscoveryReport> DiscoverAsync(string projectId, CancellationToken ct)
    {
        var (root, files) = Open(projectId);
        return await _discoverer.DiscoverAsync(projectId, root, files, ct);
    }

    public async Task<ConfigurationPlan> PlanAsync(string projectId, PlanOptions options, CancellationToken ct)
    {
        var (root, files) = Open(projectId);
        var replanned = await ReplanAsync(projectId, root, files, options, ct);
        lock (_plansGate)
        {
            _plans[replanned.Plan.PlanId] = new PlanSession(projectId, root, options, replanned.Plan, replanned.Composed);
            if (_plans.Count > 20) _plans.Remove(_plans.Keys.First());
        }
        return replanned.Plan;
    }

    private async Task<Replanned> ReplanAsync(string projectId, string root, IProjectFiles files, PlanOptions options, CancellationToken ct)
    {
        var discovery = await _discoverer.DiscoverAsync(projectId, root, files, ct);
        var (declared, declaredError) = DeclaredConfig.TryRead(files);

        ComposeResult composed;
        if (declaredError is not null)
        {
            composed = new ComposeResult("", DecisionOrigins.Baseline, [], [], [],
                [new(LimitationCodes.InvalidDeclaredConfig, declaredError + " Corrija o arquivo e planeje de novo.")], [], []);
        }
        else
        {
            composed = HarnessComposer.Compose(discovery, files, declared, ProposalReader.Read(files), options);
        }

        var plan = PlanBuilder.Build(projectId, discovery, files, composed, ManagedLockFile.Read(files));
        return new Replanned(plan, composed, discovery);
    }

    public IReadOnlyList<RunSummary> ListRuns(string projectId)
    {
        Open(projectId);
        return store.ListJournals(projectId).OrderByDescending(j => j.StartedAt)
            .Select(j => new RunSummary(j.RunId, j.Kind, j.State, j.StartedAt, j.ChangesApplied && !j.RolledBack, j.RevertedByRunId is not null)).ToList();
    }

    public JsonObject Snapshot(string runId)
    {
        var run = tracker.Find(runId) ?? throw new EngineException("RUN_NOT_FOUND", "Execução não encontrada.");
        return run.Snapshot();
    }

    public bool Cancel(string runId) => tracker.Cancel(runId);

    // ------------------------------------------------------------------ Execuções

    public RunHandle StartApply(string projectId, string planId, ApplyApprovals approvals)
    {
        var (root, files) = Open(projectId);
        PlanSession? session;
        lock (_plansGate) _plans.TryGetValue(planId, out session);
        if (session is null || session.ProjectId != projectId)
            throw new EngineException("PLAN_UNKNOWN", "O plano não está mais disponível. Gere o plano de novo.");
        if (session.Plan.IsEmpty && session.Plan.Commands.Count == 0 && session.Plan.Changes.All(c => c.Action == ChangeAction.Preserve) && session.Plan.AiEnvironment.Length == 0)
            throw new EngineException("NOTHING_TO_APPLY", "O plano não tem o que aplicar.");

        var run = Start(projectId, RunKinds.Apply);
        run.Completion = Task.Run(async () =>
        {
            var result = await _apply.ExecuteAsync(run, root, files, session, approvals,
                ct => ReplanAsync(projectId, root, files, session.Options, ct), run.Cancellation.Token,
                // O pedido de atualizar o TLC vale uma vez: depois de aplicado, a idempotência é medida sem ele.
                ct => ReplanAsync(projectId, root, files, session.Options with { UpdateTlc = false }, ct));
            run.Complete(result["outcome"]?.GetValue<string>() ?? RunStates.Failed, result);
        });
        return run;
    }

    public RunHandle StartValidate(string projectId)
    {
        var (root, files) = Open(projectId);
        var run = Start(projectId, RunKinds.Validate);
        run.Completion = Task.Run(async () =>
        {
            var journal = new RunJournal { RunId = run.RunId, ProjectId = projectId, Kind = RunKinds.Validate, StartedAt = clock.UtcNow };
            try
            {
                run.SetState(RunStates.Validating);
                var report = await ValidationService.RunAsync(runner, store, files, root, projectId,
                    ct => ReplanAsync(projectId, root, files, new PlanOptions(null), ct), run.Cancellation.Token);
                journal.Evals = report.Results.Select(r => new EvalSummary { Id = r.Id, Status = r.Status.ToString(), Required = r.Required }).ToList();
                var result = ResultJson.BuildValidation(run.RunId, projectId, report);
                journal.State = result["outcome"]!.GetValue<string>();
                journal.FinishedAt = clock.UtcNow;
                store.SaveJournal(journal);
                run.Complete(journal.State, result);
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
        run.Completion = Task.Run(async () =>
        {
            try
            {
                var result = await _revert.ExecuteAsync(run, files, targetRunId, run.Cancellation.Token);
                run.Complete(result["outcome"]?.GetValue<string>() ?? RunStates.Failed, result);
            }
            catch (EngineException ex)
            {
                run.Complete(RunStates.Failed, new JsonObject
                {
                    ["runId"] = run.RunId, ["outcome"] = RunStates.Failed, ["kind"] = RunKinds.Revert,
                    ["error"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message }
                });
            }
        });
        return run;
    }

    // ------------------------------------------------------------------ Apoio

    private RunHandle Start(string projectId, string kind)
    {
        if (!tracker.TryStart(projectId, kind, out var run) || run is null)
            throw new EngineException("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");
        return run;
    }

    private (string Root, IProjectFiles Files) Open(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root))
            throw new EngineException("PROJECT_NOT_AUTHORIZED", "O projeto não foi autorizado. Selecione a pasta pelo diálogo do aplicativo.");
        return (root, filesFactory.Open(root));
    }

    public static JsonNode? ToJson<T>(T value) => JsonSerializer.SerializeToNode(value, JournalJson.Options);
}
