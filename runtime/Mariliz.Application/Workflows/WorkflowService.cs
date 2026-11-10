using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
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
    IWorkflowContext context,
    IModelAccess? models = null,
    IEvalRecords? evalRecords = null,
    IActionExecutor? actions = null)
{
    public const string Kind = "workflow";
    public const string WorkflowId = WorkflowRequests.GraphId + ".v" + WorkflowRequests.GraphVersion;

    /// <summary>Prazo de cada conversa com o runtime de IA.</summary>
    internal TimeSpan Deadline { get; init; } = WorkflowRequests.RuntimeDeadline;

    private readonly ApprovalGate _approvals = new();
    private readonly WorkflowJournal _journal = new(store, clock);
    private readonly WorkflowEvents _events = new(clock);

    public async Task<RunHandle> StartAsync(string projectId, JsonObject input, CancellationToken ct)
    {
        EnsureAuthorized(projectId);
        await runtime.EnsureReadyAsync(ct);
        var request = WorkflowInput.Parse(input);
        var useModel = WorkflowInput.WantsModel(input);
        if (useModel && models is not { Configured: true })
            throw new EngineException("MODEL_NOT_CONFIGURED", "Configure um modelo antes de pedir uma execução com modelo.");

        if (!tracker.TryStart(projectId, Kind, out var run) || run is null)
            throw EngineErrors.RunInProgress();

        Record(run);
        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        run.Start(() => ExecuteAsync(run, executionId, request, useModel, ActionGovernance.AllowedKinds(request)));
        return run;
    }

    /// <summary>
    /// Retoma uma execução pausada. A decisão é conferida contra a aprovação que o motor abriu (identificador, hash das ações e
    /// lista do que foi proposto) antes de qualquer coisa mudar; sem <paramref name="decision"/>, nenhuma ação é aprovada.
    /// </summary>
    public Task<RunHandle> ResumeAsync(string projectId, string runId, ResumeDecision? decision = null)
    {
        EnsureAuthorized(projectId);
        decision ??= ResumeDecision.None;

        var run = tracker.Find(runId);
        if (run is null) return ResumeFromJournal(projectId, runId, decision);
        if (run.ProjectId != projectId)
            throw EngineErrors.RunNotFound();
        var released = _approvals.TryRelease(runId, pending => ApprovalBinding.EnsureDecision(pending, decision.ApprovalId, decision.ActionIds), decision.ActionIds);
        if (!released)
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
    private Task<RunHandle> ResumeFromJournal(string projectId, string runId, ResumeDecision decision)
    {
        var journal = store.LoadJournal(projectId, runId);
        if (journal is null || journal.Kind != Kind || journal.State != RunStates.AwaitingApproval
            || string.IsNullOrEmpty(journal.ExecutionId) || string.IsNullOrEmpty(journal.InterruptId) || string.IsNullOrEmpty(journal.ApprovalId))
            throw EngineErrors.RunNotFound();

        // Antes de religar qualquer coisa: o diário em disco precisa ainda bater com a aprovação que foi aberta.
        ApprovalBinding.EnsureDecision(journal, decision.ApprovalId, decision.ActionIds);

        var run = tracker.TryRehydrate(runId, projectId, Kind)
            ?? throw EngineErrors.RunInProgress();

        var published = store.LoadEvents(projectId, runId);
        if (published.Count > 0 && published[^1]["seq"] is JsonValue last)
            run.ContinueFrom(last.GetValue<int>() + 1);
        Record(run);
        ActionGovernance.ApplyDecision(journal, decision.ActionIds);
        _journal.Approve(journal);
        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        run.Start(() => ContinueFromCheckpointAsync(run, journal));
        return Task.FromResult(run);
    }

    private Task ExecuteAsync(RunHandle run, string executionId, JsonObject input, bool useModel, IReadOnlyList<string> allowedActions)
    {
        var journal = _journal.Begin(run.RunId, run.ProjectId, executionId);
        var filter = new AiEventFilter(executionId);
        return GuardedAsync(run, journal, async () =>
        {
            run.SetState(RunStates.Running, "Iniciando a execução");
            var ack = await WithDeadlineAsync(run, async ct =>
            {
                var projectContext = await context.BuildAsync(run.ProjectId, ct);
                // O acesso ao modelo vale só para esta conversa: o que foi gasto é contado pelo motor e fica no diário.
                var grant = useModel ? models!.Open(executionId, WorkflowRequests.ModelTokenBudget) : null;
                try
                {
                    return await runtime.StartAsync(WorkflowRequests.Start(run, executionId, input, projectContext, grant, allowedActions),
                        _events.Relay(run, filter, () => models?.Calls(executionId) ?? []), ct);
                }
                finally
                {
                    if (grant is not null) _journal.RecordModelUsage(journal, models!.Close(executionId));
                }
            });
            EnsureAck(run, executionId, ack);
            RecordJudgment(run, journal, ack);

            if (ack.Status == AckStatus.Interrupted)
            {
                ReviewActions(journal, ack, allowedActions);
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

    /// <summary>
    /// A opinião do juiz só vale se o proxy do motor viu a chamada do juiz. Ela é gravada no diário antes da pausa (a retomada,
    /// mesmo depois de um reinício, não depende do proxy) e no registro de avaliações, com modelo, versão e hash do pedido.
    /// </summary>
    private void RecordJudgment(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        var judgment = JudgmentVerifier.Verify(ack, models?.Calls(ack.ExecutionId) ?? []);
        if (judgment is null) return;
        _journal.RecordJudgment(journal, judgment);
        evalRecords?.Save(run.ProjectId, ack.ExecutionId, JudgmentRecord.ForDisk(judgment, run.RunId, run.ProjectId, ack.ExecutionId, clock.UtcNow));
    }

    /// <summary>
    /// As propostas do runtime passam pela política do motor antes de qualquer pessoa as ver: o que não é permitido some, e o
    /// conteúdo de cada ação aceita é escrito aqui e ligado por hash à aprovação.
    /// </summary>
    private static void ReviewActions(RunJournal journal, WorkflowAck ack, IReadOnlyList<string> allowedActions)
    {
        journal.Actions = ActionGovernance.Review(ack, allowedActions);
        journal.ActionsHash = ApprovalBinding.HashOf(journal.Actions);
    }

    private JsonObject Resume(RunHandle run, RunJournal journal) => WorkflowRequests.Resume(run, journal, context.DataRoot);

    /// <summary>
    /// A aprovação é registrada antes de ser anunciada: um <c>workflow.resume</c> que chegue logo depois do evento já a
    /// encontra. A espera é humana e não tem prazo; só o cancelamento da execução a interrompe.
    /// </summary>
    private async Task WaitForApprovalAsync(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        var approvedActions = await _approvals.WaitAsync(run.RunId, journal, run.Cancellation.Token, () => OpenApproval(run, journal, ack));
        ActionGovernance.ApplyDecision(journal, approvedActions);
        _journal.Approve(journal);
    }

    /// <summary>A pausa só vale se o runtime disse qual interrupção espera; o motor abre a aprovação e grava antes de publicar.</summary>
    private void OpenApproval(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.InterruptId is null)
            throw EngineErrors.AiRuntimeInvalidResponse();
        journal.InterruptId = ack.InterruptId;
        journal.ApprovalId = ApprovalBinding.ApprovalIdFor(run.RunId, ack.ExecutionId, ack.InterruptId, journal.ActionsHash ?? ApprovalBinding.HashOf(journal.Actions));
        _journal.Mark(journal, RunStates.AwaitingApproval);
        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        _events.ApprovalRequired(run, journal.ApprovalId, ack.ExecutionId, WorkflowResult.StepIds(ack.Output), ActionGovernance.ForEvent(journal.Actions));
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
    }

    private void Record(RunHandle run) => run.Recorder = evt => store.AppendEvent(run.ProjectId, run.RunId, evt);

    private void Finish(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.Status == AckStatus.Interrupted)
            throw EngineErrors.AiRuntimeInvalidResponse();

        var outcome = _journal.Conclude(journal, ack, actions);
        run.Complete(outcome, WorkflowResult.From(ack, journal.ModelUsage, journal.Judgment, journal.Actions));
        run.Publish(ack.Status == AckStatus.Failed ? "workflow.failed" : "workflow.completed", _events.Status(ack.ExecutionId, ack.Status.ToWire()));
    }

    private void Fail(RunHandle run, RunJournal journal, string code, string message)
    {
        _journal.Fail(journal, code);
        run.Complete(RunStates.Failed, WorkflowResult.Failure(code, message));
    }

    private async Task Abandon(RunHandle run, RunJournal journal)
    {
        _approvals.Discard(run.RunId);
        var stored = store.LoadJournal(run.ProjectId, run.RunId);
        if (stored is not null && RunStates.IsTerminal(stored.State))
        {
            run.Complete(stored.State, new JsonObject { ["kind"] = Kind });
            return;
        }

        await runtime.CancelAsync(run.RunId, run.ProjectId, context.DataRoot);
        _journal.Cancel(journal);
        run.Complete(RunStates.Cancelled, new JsonObject { ["kind"] = Kind });
    }
}
