using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Ciclo de vida do grafo <c>engineering.request.v1</c>: autoriza, inicia, espera a aprovação humana, retoma e termina,
/// mantendo o diário de cada execução. O pedido, as mensagens para o runtime, os eventos, o resultado e o contexto do
/// projeto têm classes próprias nesta pasta. O runtime de IA não grava no projeto.
/// </summary>
public sealed class WorkflowService(
    WorkspaceAuthorizer workspace,
    IAiRuntime runtime,
    RunTracker tracker,
    IClock clock,
    IRunStore store,
    WorkflowContextBuilder context)
{
    public const string Kind = "workflow";
    public const string WorkflowId = WorkflowRequests.GraphId + ".v" + WorkflowRequests.GraphVersion;

    /// <summary>Prazo de cada conversa com o runtime de IA.</summary>
    internal TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _approvals = new();
    private readonly WorkflowEvents _events = new(clock);

    public async Task<RunHandle> StartAsync(string projectId, JsonObject input, CancellationToken ct)
    {
        EnsureAuthorized(projectId);
        await runtime.EnsureReadyAsync(ct);
        var request = WorkflowInput.Parse(input);

        if (!tracker.TryStart(projectId, Kind, out var run) || run is null)
            throw EngineErrors.RunInProgress();

        Record(run);
        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        run.Start(() => ExecuteAsync(run, executionId, request));
        return run;
    }

    public Task<RunHandle> ResumeAsync(string projectId, string runId)
    {
        EnsureAuthorized(projectId);

        var run = tracker.Find(runId);
        if (run is null) return ResumeFromJournal(projectId, runId);
        if (run.ProjectId != projectId)
            throw EngineErrors.RunNotFound();
        if (!_approvals.TryGetValue(runId, out var gate) || !gate.TrySetResult(true))
            throw new EngineException("WORKFLOW_NOT_INTERRUPTED", "Não há uma execução aguardando aprovação.");
        return Task.FromResult(run);
    }

    public Task ShutdownAsync() => runtime.ShutdownAsync();

    public string AiRuntimeStatus() => runtime.Probe();

    private void EnsureAuthorized(string projectId)
    {
        if (!workspace.IsAuthorized(projectId))
            throw EngineErrors.ProjectNotAuthorized();
    }

    /// <summary>O motor caiu com a execução pausada: ela volta do diário, com a numeração de eventos que já tinha.</summary>
    private Task<RunHandle> ResumeFromJournal(string projectId, string runId)
    {
        var journal = store.LoadJournal(projectId, runId);
        if (journal is null || journal.Kind != Kind || journal.State != RunStates.AwaitingApproval
            || string.IsNullOrEmpty(journal.ExecutionId) || string.IsNullOrEmpty(journal.InterruptId) || string.IsNullOrEmpty(journal.ApprovalId))
            throw EngineErrors.RunNotFound();

        var run = tracker.TryRehydrate(runId, projectId, Kind)
            ?? throw EngineErrors.RunInProgress();

        var published = store.LoadEvents(projectId, runId);
        if (published.Count > 0 && published[^1]["seq"] is JsonValue last)
            run.ContinueFrom(last.GetValue<int>() + 1);
        Record(run);
        Approve(journal);
        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        run.Start(() => ContinueFromCheckpointAsync(run, journal));
        return Task.FromResult(run);
    }

    private Task ExecuteAsync(RunHandle run, string executionId, JsonObject input)
    {
        var journal = BeginJournal(run, executionId);
        var filter = new AiEventFilter(executionId);
        return GuardedAsync(run, journal, async () =>
        {
            run.SetState(RunStates.Running, "Iniciando a execução");
            var ack = await WithDeadlineAsync(run, async ct =>
            {
                var projectContext = await context.BuildAsync(run.ProjectId, ct);
                return await runtime.StartAsync(WorkflowRequests.Start(run, executionId, input, projectContext), _events.Relay(run, filter), ct);
            });
            EnsureAck(run, executionId, ack);

            if (ack.Status == "interrupted")
            {
                await WaitForApprovalAsync(run, journal, ack);
                ack = await WithDeadlineAsync(run, ct => runtime.ResumeAsync(Resume(run, journal), _events.Relay(run, filter), ct));
                EnsureAck(run, executionId, ack);
            }

            Finish(run, journal, ack);
        });
    }

    private Task ContinueFromCheckpointAsync(RunHandle run, RunJournal journal) => GuardedAsync(run, journal, async () =>
    {
        run.SetState(RunStates.Running, "Retomando a execução");
        var filter = new AiEventFilter(journal.ExecutionId!);
        var ack = await WithDeadlineAsync(run, ct => runtime.ResumeAsync(Resume(run, journal), _events.Relay(run, filter), ct));
        EnsureAck(run, journal.ExecutionId!, ack);
        Finish(run, journal, ack);
    });

    private JsonObject Resume(RunHandle run, RunJournal journal) => WorkflowRequests.Resume(run, journal, context.DataRoot);

    /// <summary>
    /// A aprovação é registrada antes de ser anunciada: um <c>workflow.resume</c> que chegue logo depois do evento já a
    /// encontra. A espera é humana e não tem prazo; só o cancelamento da execução a interrompe.
    /// </summary>
    private async Task WaitForApprovalAsync(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _approvals[run.RunId] = gate;
        try
        {
            OpenApproval(run, journal, ack);
            using (run.Cancellation.Token.Register(() => gate.TrySetCanceled(run.Cancellation.Token)))
                await gate.Task;
        }
        finally
        {
            _approvals.TryRemove(run.RunId, out _);
        }
        Approve(journal);
    }

    /// <summary>A pausa só vale se o runtime disse qual interrupção espera; o motor abre a aprovação e grava antes de publicar.</summary>
    private void OpenApproval(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.InterruptId is null)
            throw EngineErrors.AiRuntimeInvalidResponse();
        journal.InterruptId = ack.InterruptId;
        journal.ApprovalId = "apr-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        Mark(journal, RunStates.AwaitingApproval);
        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        _events.ApprovalRequired(run, journal.ApprovalId, ack.ExecutionId, WorkflowResult.StepIds(ack.Output));
    }

    /// <summary>Cada conversa com o runtime tem o próprio prazo, que não conta o tempo em que a execução esperou alguém.</summary>
    private async Task<T> WithDeadlineAsync<T>(RunHandle run, Func<CancellationToken, Task<T>> step)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(run.Cancellation.Token);
        budget.CancelAfter(Deadline);
        return await step(budget.Token);
    }

    /// <summary>Toda saída da execução passa por aqui e termina o diário: nenhum erro deixa a execução presa.</summary>
    private async Task GuardedAsync(RunHandle run, RunJournal journal, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (OperationCanceledException) when (!run.Cancellation.IsCancellationRequested)
        {
            await runtime.CancelAsync(run.RunId, run.ProjectId, context.DataRoot);
            Fail(run, journal, "AI_RUNTIME_TIMEOUT", "O runtime de IA passou do tempo limite.");
        }
        catch (OperationCanceledException)
        {
            await Abandon(run, journal);
        }
        catch (EngineException ex)
        {
            Fail(run, journal, ex.Code, ex.Message);
        }
        catch (JsonException)
        {
            Fail(run, journal, "AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            Fail(run, journal, "AI_RUNTIME_UNAVAILABLE", "O runtime de IA não respondeu.");
        }
        catch (Exception ex)
        {
            // O texto da exceção pode ter caminhos do projeto: só o tipo vai para o diário e para a interface.
            Fail(run, journal, "WORKFLOW_FAILED", $"Falha interna ({ex.GetType().Name}).");
        }
    }

    private static void EnsureAck(RunHandle run, string executionId, WorkflowAck ack)
    {
        if (ack.RunId != run.RunId || ack.ProjectId != run.ProjectId || ack.ExecutionId != executionId || ack.WorkflowId != WorkflowId)
            throw EngineErrors.AiRuntimeInvalidResponse();
        if (ack.Status is not ("completed" or "interrupted" or "failed"))
            throw EngineErrors.AiRuntimeInvalidResponse();
    }

    private void Record(RunHandle run) => run.Recorder = evt => store.AppendEvent(run.ProjectId, run.RunId, evt);

    private RunJournal BeginJournal(RunHandle run, string executionId)
    {
        var journal = new RunJournal
        {
            RunId = run.RunId,
            ProjectId = run.ProjectId,
            Kind = Kind,
            ExecutionId = executionId,
            State = RunStates.Running,
            StartedAt = clock.UtcNow
        };
        store.SaveJournal(journal);
        return journal;
    }

    private void Mark(RunJournal journal, string state)
    {
        journal.State = state;
        store.SaveJournal(journal);
    }

    private void Approve(RunJournal journal)
    {
        journal.ApprovedAt = clock.UtcNow;
        store.SaveJournal(journal);
    }

    private void Finish(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.Status == "interrupted")
            throw EngineErrors.AiRuntimeInvalidResponse();

        var outcome = ack.Status == "failed" ? RunStates.Failed : RunStates.CompletedWithLimitations;
        journal.State = outcome;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = outcome == RunStates.Failed ? (ack.Errors.FirstOrDefault() ?? ack.Limitations.FirstOrDefault())?.Code : null;
        journal.Evals = ack.Limitations.Select(item => new EvalSummary
        {
            Id = item.Code,
            Status = item.Code == "JUDGE_NOT_RUN" ? "not_run" : "recorded",
            Required = false
        }).ToList();
        store.SaveJournal(journal);
        run.Complete(outcome, WorkflowResult.From(ack));
        run.Publish(ack.Status == "failed" ? "workflow.failed" : "workflow.completed", _events.Status(ack.ExecutionId, ack.Status));
    }

    private void Fail(RunHandle run, RunJournal journal, string code, string message)
    {
        journal.State = RunStates.Failed;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = code;
        store.SaveJournal(journal);
        run.Complete(RunStates.Failed, WorkflowResult.Failure(code, message));
    }

    private async Task Abandon(RunHandle run, RunJournal journal)
    {
        _approvals.TryRemove(run.RunId, out _);
        var stored = store.LoadJournal(run.ProjectId, run.RunId);
        if (stored is not null && RunStates.IsTerminal(stored.State))
        {
            run.Complete(stored.State, new JsonObject { ["kind"] = Kind });
            return;
        }

        await runtime.CancelAsync(run.RunId, run.ProjectId, context.DataRoot);
        journal.State = RunStates.Cancelled;
        journal.FinishedAt = clock.UtcNow;
        store.SaveJournal(journal);
        run.Complete(RunStates.Cancelled, new JsonObject { ["kind"] = Kind });
    }
}
