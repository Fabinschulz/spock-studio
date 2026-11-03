using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Execução do grafo <c>engineering.request.v1</c>. O motor autoriza, descreve o projeto, indexa
/// as fontes permitidas fora da pasta, fixa limites e política, guarda a aprovação e publica os eventos
/// à medida que o runtime os transmite. O runtime não grava no projeto.
/// </summary>
public sealed class WorkflowService(
    WorkspaceAuthorizer workspace,
    IAiRuntime runtime,
    RunTracker tracker,
    IClock clock,
    IProjectFilesFactory? filesFactory = null,
    IProcessRunner? processRunner = null,
    string? knowledgeRoot = null,
    IRunStore? store = null)
{
    public const string Kind = "workflow";
    public const string WorkflowId = "engineering.request.v1";
    private const string GraphId = "engineering.request";
    private const string GraphVersion = "1";
    private const int MaxGoal = 4000;
    private static readonly HashSet<string> KnownAgents = new(StringComparer.Ordinal) { "architecture" };
    private static readonly HashSet<string> InputFields = new(StringComparer.Ordinal) { "goal", "impact", "requiresApproval" };

    /// <summary>Prazo de cada conversa com o runtime de IA.</summary>
    internal TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _approvals = new();
    private readonly ProjectDiscoverer? _discoverer = filesFactory is not null && processRunner is not null
        ? new ProjectDiscoverer(processRunner)
        : null;

    public async Task<RunHandle> StartAsync(string projectId, JsonObject input, CancellationToken ct)
    {
        if (!workspace.IsAuthorized(projectId))
            throw new EngineException("PROJECT_NOT_AUTHORIZED", "O projeto não foi autorizado. Selecione a pasta pelo diálogo do aplicativo.");

        await runtime.EnsureReadyAsync(ct);
        var request = Input(input);

        if (!tracker.TryStart(projectId, Kind, out var run) || run is null)
            throw new EngineException("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");

        Record(run);
        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        run.Start(() => ExecuteAsync(run, executionId, request));
        return run;
    }

    public Task<RunHandle> ResumeAsync(string projectId, string runId)
    {
        if (!workspace.IsAuthorized(projectId))
            throw new EngineException("PROJECT_NOT_AUTHORIZED", "O projeto não foi autorizado. Selecione a pasta pelo diálogo do aplicativo.");

        var run = tracker.Find(runId);
        if (run is not null)
        {
            if (run.ProjectId != projectId)
                throw new EngineException("RUN_NOT_FOUND", "Execução não encontrada.");
            if (!_approvals.TryGetValue(runId, out var gate) || !gate.TrySetResult(true))
                throw new EngineException("WORKFLOW_NOT_INTERRUPTED", "Não há uma execução aguardando aprovação.");
            return Task.FromResult(run);
        }

        return ResumeFromJournal(projectId, runId);
    }

    public Task ShutdownAsync() => runtime.ShutdownAsync();

    public string AiRuntimeStatus() => runtime.Probe();

    private Task<RunHandle> ResumeFromJournal(string projectId, string runId)
    {
        var journal = store?.LoadJournal(projectId, runId);
        if (journal is null || journal.Kind != Kind || journal.State != RunStates.AwaitingApproval
            || string.IsNullOrEmpty(journal.ExecutionId) || string.IsNullOrEmpty(journal.InterruptId) || string.IsNullOrEmpty(journal.ApprovalId))
            throw new EngineException("RUN_NOT_FOUND", "Execução não encontrada.");

        var run = tracker.TryRehydrate(runId, projectId, Kind);
        if (run is null)
            throw new EngineException("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");

        var published = store!.LoadEvents(projectId, runId);
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
                var context = await ContextAsync(run.ProjectId, ct);
                await AttachKnowledgeAsync(run.ProjectId, context, ct);
                StampExecutionRoot(context);
                return await runtime.StartAsync(StartRequest(run, executionId, input, context), Relay(run, filter), ct);
            });
            EnsureAck(run, executionId, ack);

            if (ack.Status == "interrupted")
            {
                await WaitForApprovalAsync(run, journal, ack);
                ack = await WithDeadlineAsync(run, ct => runtime.ResumeAsync(ResumeRequest(run, journal), Relay(run, filter), ct));
                EnsureAck(run, executionId, ack);
            }

            Finish(run, journal, ack);
        });
    }

    private Task ContinueFromCheckpointAsync(RunHandle run, RunJournal journal) => GuardedAsync(run, journal, async () =>
    {
        run.SetState(RunStates.Running, "Retomando a execução");
        var filter = new AiEventFilter(journal.ExecutionId!);
        var ack = await WithDeadlineAsync(run, ct => runtime.ResumeAsync(ResumeRequest(run, journal), Relay(run, filter), ct));
        EnsureAck(run, journal.ExecutionId!, ack);
        Finish(run, journal, ack);
    });

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
            await runtime.CancelAsync(run.RunId, run.ProjectId, knowledgeRoot);
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

    /// <summary>O pedido entra só com os campos do contrato; o objetivo é obrigatório e limitado.</summary>
    private static JsonObject Input(JsonObject input)
    {
        if (input.Any(pair => !InputFields.Contains(pair.Key)))
            throw new EngineException(ProtocolErrorCodes.InvalidParams, "'input' só aceita goal, impact e requiresApproval.");
        var goal = input["goal"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : "";
        if (goal.Length is 0 or > MaxGoal)
            throw new EngineException(ProtocolErrorCodes.InvalidParams, $"Descreva o pedido em até {MaxGoal} caracteres.");
        var request = new JsonObject { ["goal"] = goal };
        foreach (var flag in new[] { "impact", "requiresApproval" })
        {
            if (input[flag] is null) continue;
            if (input[flag] is not JsonValue item || !item.TryGetValue<bool>(out var on))
                throw new EngineException(ProtocolErrorCodes.InvalidParams, $"'{flag}' precisa ser verdadeiro ou falso.");
            request[flag] = on;
        }
        return request;
    }

    private static JsonObject StartRequest(RunHandle run, string executionId, JsonObject input, JsonObject context) => new()
    {
        ["schema"] = "ai.workflow.start.v2",
        ["workflowId"] = GraphId,
        ["workflowVersion"] = GraphVersion,
        ["runId"] = run.RunId,
        ["executionId"] = executionId,
        ["projectId"] = run.ProjectId,
        ["workspaceId"] = null,
        ["input"] = input.DeepClone(),
        ["context"] = context,
        ["limits"] = new JsonObject { ["maxIterations"] = 8, ["maxMilliseconds"] = 10_000, ["maxTokens"] = 0, ["maxCostUsd"] = 0 },
        ["policy"] = new JsonObject { ["allowedActions"] = new JsonArray(), ["modelAllowed"] = false }
    };

    private JsonObject ResumeRequest(RunHandle run, RunJournal journal) => new()
    {
        ["schema"] = "ai.workflow.resume.v2",
        ["runId"] = run.RunId,
        ["executionId"] = journal.ExecutionId,
        ["projectId"] = run.ProjectId,
        ["interruptId"] = journal.InterruptId,
        ["decision"] = new JsonObject { ["approvalId"] = journal.ApprovalId, ["approved"] = true, ["actionIds"] = new JsonArray() },
        ["context"] = new JsonObject { ["executionRoot"] = knowledgeRoot ?? "" }
    };

    /// <summary>A pausa só vale se o runtime disse qual interrupção espera; o motor abre a aprovação e grava antes de publicar.</summary>
    private void OpenApproval(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.InterruptId is null)
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
        journal.InterruptId = ack.InterruptId;
        journal.ApprovalId = "apr-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        Mark(journal, RunStates.AwaitingApproval);
        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        run.Publish("approval.required", new JsonObject
        {
            ["approvalId"] = journal.ApprovalId,
            ["executionId"] = ack.ExecutionId,
            ["actions"] = new JsonArray(),
            ["at"] = Now()
        });
        var paused = EventData(ack.ExecutionId, "interrupted");
        paused["steps"] = StepIds(ack.Output);
        run.Publish("workflow.interrupted", paused);
    }

    private void Approve(RunJournal journal)
    {
        journal.ApprovedAt = clock.UtcNow;
        store?.SaveJournal(journal);
    }

    private Action<JsonObject> Relay(RunHandle run, AiEventFilter filter) => evt =>
    {
        if (filter.Accept(evt)) Translate(run, evt);
    };

    /// <summary>Traduz o evento do runtime para o vocabulário da interface. O que não tem tradução segura fica de fora.</summary>
    private void Translate(RunHandle run, JsonObject evt)
    {
        var data = (JsonObject)evt["data"]!;
        var agentId = Text(evt, "agentId");
        switch (Text(evt, "type"))
        {
            case "workflow.started":
                run.Publish("workflow.started", EventData(Text(evt, "executionId"), "started"));
                break;
            case "context.retrieval.started":
                run.Publish("context.retrieval.started", new JsonObject { ["at"] = Now() });
                break;
            case "context.retrieved" when TryEvidence(data, out var evidence):
                evidence["at"] = Now();
                run.Publish("context.retrieved", evidence);
                break;
            case "agent.started" when KnownAgents.Contains(agentId) && IsVersion(Text(data, "agentVersion")):
                run.Publish("agent.started", new JsonObject { ["agentId"] = agentId, ["agentVersion"] = Text(data, "agentVersion"), ["at"] = Now() });
                break;
            case "agent.completed" when KnownAgents.Contains(agentId) && Text(data, "status") == "completed":
                run.Publish("agent.completed", new JsonObject { ["agentId"] = agentId, ["at"] = Now() });
                break;
        }
    }

    private void Record(RunHandle run)
    {
        if (store is not null)
            run.Recorder = evt => store.AppendEvent(run.ProjectId, run.RunId, evt);
    }

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
        store?.SaveJournal(journal);
        return journal;
    }

    private void Mark(RunJournal journal, string state)
    {
        journal.State = state;
        store?.SaveJournal(journal);
    }

    private void Finish(RunHandle run, RunJournal journal, WorkflowAck ack)
    {
        if (ack.Status == "interrupted")
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");

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
        store?.SaveJournal(journal);
        var result = Result(ack);
        run.Complete(outcome, result);
        run.Publish(ack.Status == "failed" ? "workflow.failed" : "workflow.completed", EventData(ack.ExecutionId, ack.Status));
    }

    private void Fail(RunHandle run, RunJournal journal, string code, string message)
    {
        journal.State = RunStates.Failed;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = code;
        store?.SaveJournal(journal);
        run.Complete(RunStates.Failed, Failure(code, message));
    }

    private async Task Abandon(RunHandle run, RunJournal journal)
    {
        _approvals.TryRemove(run.RunId, out _);
        var stored = store?.LoadJournal(run.ProjectId, run.RunId);
        if (stored is not null && RunStates.IsTerminal(stored.State))
        {
            run.Complete(stored.State, new JsonObject { ["kind"] = Kind });
            return;
        }

        await runtime.CancelAsync(run.RunId, run.ProjectId, knowledgeRoot);
        journal.State = RunStates.Cancelled;
        journal.FinishedAt = clock.UtcNow;
        store?.SaveJournal(journal);
        run.Complete(RunStates.Cancelled, new JsonObject { ["kind"] = Kind });
    }

    private void StampExecutionRoot(JsonObject context)
    {
        if (!string.IsNullOrWhiteSpace(knowledgeRoot))
            context["executionRoot"] = knowledgeRoot;
    }

    private async Task<JsonObject> ContextAsync(string projectId, CancellationToken ct)
    {
        if (_discoverer is null || filesFactory is null || !workspace.TryGetRoot(projectId, out var root))
            return new JsonObject { ["available"] = false, ["stacks"] = new JsonArray() };

        try
        {
            var report = await _discoverer.DiscoverAsync(projectId, root, filesFactory.Open(root), ct);
            return new JsonObject
            {
                ["available"] = true,
                ["stacks"] = new JsonArray(report.Stacks.Select(stack => stack.Id).Distinct().Take(8).Select(id => (JsonNode)JsonValue.Create(id)!).ToArray())
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return new JsonObject { ["available"] = false, ["stacks"] = new JsonArray() };
        }
    }

    private async Task AttachKnowledgeAsync(string projectId, JsonObject context, CancellationToken ct)
    {
        if (filesFactory is null || string.IsNullOrWhiteSpace(knowledgeRoot) || !SafeProjectId(projectId)) return;
        if (!workspace.TryGetRoot(projectId, out var root)) return;

        IReadOnlyDictionary<string, string> indexed;
        try
        {
            Directory.CreateDirectory(knowledgeRoot);
            indexed = await runtime.IndexedHashesAsync(projectId, knowledgeRoot, ct);
        }
        catch (Exception ex) when (ex is EngineException or HttpRequestException or IOException)
        {
            indexed = new Dictionary<string, string>();
        }

        var sources = new JsonArray();
        foreach (var source in KnowledgeSources.Select(filesFactory.Open(root), indexed))
        {
            var item = new JsonObject { ["path"] = source.Path, ["hash"] = source.Hash };
            if (source.Text is not null) item["text"] = source.Text;
            sources.Add(item);
        }
        context["knowledgeRoot"] = knowledgeRoot;
        context["sources"] = sources;
    }

    private static bool SafeProjectId(string projectId) =>
        projectId.Length == 14
        && projectId.StartsWith("p-", StringComparison.Ordinal)
        && projectId[2..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsVersion(string value) => value.Length is > 0 and <= 9 && value.All(char.IsAsciiDigit);

    private string Now() => clock.UtcNow.ToString("O");

    private JsonObject EventData(string executionId, string status) => new()
    {
        ["workflowId"] = WorkflowId,
        ["executionId"] = executionId,
        ["status"] = status,
        ["at"] = Now()
    };

    private static void EnsureAck(RunHandle run, string executionId, WorkflowAck ack)
    {
        if (ack.RunId != run.RunId || ack.ProjectId != run.ProjectId || ack.ExecutionId != executionId || ack.WorkflowId != WorkflowId)
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
        if (ack.Status is not ("completed" or "interrupted" or "failed"))
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
    }

    private static JsonObject Result(WorkflowAck ack)
    {
        var summary = ack.Output["summary"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        var result = new JsonObject
        {
            ["kind"] = Kind,
            ["workflowId"] = ack.WorkflowId,
            ["executionId"] = ack.ExecutionId,
            ["summary"] = summary,
            ["stacks"] = ack.Output["stacks"]?.DeepClone() ?? new JsonArray(),
            ["evidence"] = CopyEvidence(ack.Output),
            ["limitations"] = new JsonArray(ack.Limitations.Select(item => (JsonNode)new JsonObject
            {
                ["code"] = item.Code,
                ["message"] = item.Message
            }).ToArray())
        };
        if (TryImpact(ack.Output["impact"], out var impact)) result["impact"] = impact;
        if (TryEvaluation(ack.Output["evaluation"], out var evaluation)) result["evaluation"] = evaluation;
        return result;
    }

    private static bool TryImpact(JsonNode? node, out JsonObject impact)
    {
        impact = new JsonObject();
        if (node is not JsonObject item || Text(item, "schema") != "engineering.impact.v1") return false;
        if (item["areas"] is not JsonArray areas || item["risks"] is not JsonArray risks || item["evidence"] is not JsonArray refs) return false;
        var copyAreas = new JsonArray();
        foreach (var area in areas)
        {
            if (area is not JsonValue value || !value.TryGetValue<string>(out var text) || text is not ("servico" or "interface" or "projeto")) return false;
            copyAreas.Add(text);
        }
        if (copyAreas.Count == 0) return false;
        var copyRisks = new JsonArray();
        foreach (var risk in risks)
        {
            if (risk is not JsonValue value || !value.TryGetValue<string>(out var text) || text is not ("sem-evidencia" or "sem-stack")) return false;
            copyRisks.Add(text);
        }
        var copyRefs = new JsonArray();
        foreach (var reference in refs)
        {
            if (reference is not JsonValue value || !value.TryGetValue<string>(out var text) || !SafeChunk(text)) return false;
            copyRefs.Add(text);
        }
        impact["schema"] = "engineering.impact.v1";
        impact["areas"] = copyAreas;
        impact["risks"] = copyRisks;
        impact["evidence"] = copyRefs;
        return true;
    }

    private static bool TryEvaluation(JsonNode? node, out JsonObject evaluation)
    {
        evaluation = new JsonObject();
        if (node is not JsonObject item || Text(item, "verdict") != "deterministic" || Text(item, "judge") != "not_run") return false;
        evaluation["schema"] = "engineering.evaluation.v1";
        evaluation["verdict"] = "deterministic";
        evaluation["judge"] = "not_run";
        evaluation["tokens"] = 0;
        return true;
    }

    private static bool SafeChunk(string value) =>
        value.Length is > 0 and <= 240 && !value.Contains('\\') && !value.Contains("..");

    private static JsonArray CopyEvidence(JsonObject output)
    {
        var copies = new JsonArray();
        if (output["evidence"] is not JsonArray evidence) return copies;
        foreach (var node in evidence)
        {
            if (node is not JsonObject item || !TryEvidence(item, out var data)) continue;
            var excerpt = item["excerpt"] is JsonValue value && value.TryGetValue<string>(out var text) ? SecretScanner.RedactLines(text) : "";
            if (SecretScanner.ContainsSecret(excerpt)) excerpt = "";
            data["excerpt"] = excerpt;
            copies.Add(data);
        }
        return copies;
    }

    private static bool TryEvidence(JsonObject item, out JsonObject data)
    {
        data = new JsonObject();
        var path = Text(item, "filePath");
        var chunkId = Text(item, "chunkId");
        var hash = Text(item, "hash");
        if (!SafeRelative(path) || chunkId.Length == 0 || hash.Length != 64) return false;
        if (item["score"] is not JsonValue score || !double.TryParse(score.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return false;
        data["documentId"] = Text(item, "documentId").Length == 0 ? path : Text(item, "documentId");
        data["chunkId"] = chunkId;
        data["filePath"] = path;
        data["hash"] = hash;
        data["score"] = value;
        return SafeRelative(data["documentId"]!.GetValue<string>());
    }

    private static string Text(JsonObject item, string name) =>
        item[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static bool SafeRelative(string path) =>
        path.Length is > 0 and <= 240
        && !path.Contains('\\')
        && !path.StartsWith('/')
        && !path.Split('/').Contains("..");

    private static JsonArray StepIds(JsonObject output)
    {
        var ids = new JsonArray();
        if (output["plan"]?["steps"] is not JsonArray steps) return ids;
        foreach (var step in steps)
        {
            if (step is JsonObject item && item["id"] is JsonValue value && value.TryGetValue<string>(out var id) && IsStepId(id))
                ids.Add(id);
        }
        return ids;
    }

    private static bool IsStepId(string id) =>
        id.Length is > 0 and <= 32 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static JsonObject Failure(string code, string message) => new()
    {
        ["kind"] = Kind,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };
}
