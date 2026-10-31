using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Mariliz.Application.Discovery;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Execução do grafo <c>engineering.request.v1</c>. O motor autoriza, descreve o projeto, indexa
/// as fontes permitidas fora da pasta e publica os eventos. O runtime não grava no projeto.
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

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _approvals = new();
    private readonly ProjectDiscoverer? _discoverer = filesFactory is not null && processRunner is not null
        ? new ProjectDiscoverer(processRunner)
        : null;

    public async Task<RunHandle> StartAsync(string projectId, JsonObject input, CancellationToken ct)
    {
        if (!workspace.IsAuthorized(projectId))
            throw new EngineException("PROJECT_NOT_AUTHORIZED", "O projeto não foi autorizado. Selecione a pasta pelo diálogo do aplicativo.");

        await runtime.EnsureReadyAsync(ct);

        if (!tracker.TryStart(projectId, Kind, out var run) || run is null)
            throw new EngineException("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");

        var executionId = "ex-" + Guid.NewGuid().ToString("N")[..12];
        run.Completion = ExecuteAsync(run, executionId, input);
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

    private Task<RunHandle> ResumeFromJournal(string projectId, string runId)
    {
        var journal = store?.LoadJournal(projectId, runId);
        if (journal is null || journal.Kind != Kind || journal.State != RunStates.AwaitingApproval || string.IsNullOrEmpty(journal.ExecutionId))
            throw new EngineException("RUN_NOT_FOUND", "Execução não encontrada.");

        var run = tracker.TryRehydrate(runId, projectId, Kind);
        if (run is null)
            throw new EngineException("RUN_IN_PROGRESS", "Já há uma execução em andamento neste projeto.");

        run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
        run.Completion = ContinueFromCheckpointAsync(run, journal);
        return Task.FromResult(run);
    }

    private async Task ExecuteAsync(RunHandle run, string executionId, JsonObject input)
    {
        var journal = BeginJournal(run, executionId);
        try
        {
            run.SetState(RunStates.Running, "Iniciando a execução");
            var context = await ContextAsync(run.ProjectId, run.Cancellation.Token);
            await AttachKnowledgeAsync(run.ProjectId, context, run.Cancellation.Token);
            StampExecutionRoot(context);
            var ack = await runtime.StartAsync(run.RunId, run.ProjectId, executionId, input, context, run.Cancellation.Token);
            EnsureAck(run, executionId, ack);
            Publish(run, ack, "workflow.started", "started");
            PublishRetrieval(run, ack);

            if (ack.Status == "interrupted")
            {
                Mark(journal, RunStates.AwaitingApproval);
                var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _approvals[run.RunId] = gate;
                run.SetState(RunStates.AwaitingApproval, "Aguardando aprovação");
                var paused = EventData(ack, "interrupted");
                paused["steps"] = StepIds(ack.Output);
                run.Publish("workflow.interrupted", paused);
                using (run.Cancellation.Token.Register(() => gate.TrySetCanceled(run.Cancellation.Token)))
                    await gate.Task;
                _approvals.TryRemove(run.RunId, out _);
                ack = await runtime.ResumeAsync(run.RunId, run.ProjectId, executionId, ExecutionContext(), run.Cancellation.Token);
                EnsureAck(run, executionId, ack);
            }

            Finish(run, journal, ack);
        }
        catch (OperationCanceledException)
        {
            await Abandon(run, journal);
        }
        catch (EngineException ex)
        {
            Fail(run, journal, ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            Fail(run, journal, "AI_RUNTIME_UNAVAILABLE", "O runtime de IA não respondeu.");
        }
    }

    private async Task ContinueFromCheckpointAsync(RunHandle run, RunJournal journal)
    {
        try
        {
            run.SetState(RunStates.Running, "Retomando a execução");
            var ack = await runtime.ResumeAsync(run.RunId, run.ProjectId, journal.ExecutionId!, ExecutionContext(), run.Cancellation.Token);
            EnsureAck(run, journal.ExecutionId!, ack);
            PublishRetrieval(run, ack);
            Finish(run, journal, ack);
        }
        catch (OperationCanceledException)
        {
            await Abandon(run, journal);
        }
        catch (EngineException ex)
        {
            Fail(run, journal, ex.Code, ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            Fail(run, journal, "AI_RUNTIME_UNAVAILABLE", "O runtime de IA não respondeu.");
        }
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
        Publish(run, ack, ack.Status == "failed" ? "workflow.failed" : "workflow.completed", ack.Status);
        journal.State = outcome;
        journal.FinishedAt = clock.UtcNow;
        journal.Error = outcome == RunStates.Failed ? ack.Limitations.FirstOrDefault()?.Code : null;
        journal.Evals = ack.Limitations.Select(item => new EvalSummary
        {
            Id = item.Code,
            Status = item.Code == "JUDGE_NOT_RUN" ? "not_run" : "recorded",
            Required = false
        }).ToList();
        store?.SaveJournal(journal);
        run.Complete(outcome, Result(ack));
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

    private JsonObject ExecutionContext()
    {
        var context = new JsonObject();
        StampExecutionRoot(context);
        return context;
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
                ["stacks"] = new JsonArray(report.Stacks.Select(stack => (JsonNode)JsonValue.Create(stack.Id)!).ToArray())
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

    private void PublishRetrieval(RunHandle run, WorkflowAck ack)
    {
        if (ack.Output["retrievalAttempted"] is not JsonValue flag || !flag.TryGetValue<bool>(out var attempted) || !attempted)
            return;
        run.Publish("context.retrieval.started", new JsonObject { ["at"] = clock.UtcNow.ToString("O") });
        if (ack.Output["evidence"] is not JsonArray evidence) return;
        foreach (var node in evidence)
        {
            if (node is not JsonObject item || !TryEvidence(item, out var data)) continue;
            data["at"] = clock.UtcNow.ToString("O");
            run.Publish("context.retrieved", data);
        }
    }

    private static bool SafeProjectId(string projectId) =>
        projectId.Length == 14
        && projectId.StartsWith("p-", StringComparison.Ordinal)
        && projectId[2..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void Publish(RunHandle run, WorkflowAck ack, string type, string status) =>
        run.Publish(type, EventData(ack, status));

    private JsonObject EventData(WorkflowAck ack, string status) => new()
    {
        ["workflowId"] = ack.WorkflowId,
        ["executionId"] = ack.ExecutionId,
        ["status"] = status,
        ["at"] = clock.UtcNow.ToString("O")
    };

    private static void EnsureAck(RunHandle run, string executionId, WorkflowAck ack)
    {
        if (ack.RunId != run.RunId || ack.ExecutionId != executionId || ack.WorkflowId != WorkflowId)
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
        if (ack.Status is not ("completed" or "interrupted" or "failed"))
            throw new EngineException("AI_RUNTIME_INVALID_RESPONSE", "O runtime de IA respondeu fora do contrato.");
    }

    private static JsonObject Result(WorkflowAck ack)
    {
        var summary = ack.Output["summary"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        return new JsonObject
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
    }

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
