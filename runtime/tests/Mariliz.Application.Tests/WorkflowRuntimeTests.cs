using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// O runtime Python oco confirma o início da execução. O evento sai no mesmo envelope que o host
/// reencaminha como <c>engine_event</c> e que a UI já aceita.
/// </summary>
public class WorkflowRuntimeTests
{
    [Fact]
    public async Task Runtime_ausente_recusa_o_inicio_sem_gravar_execucao()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var tracker = new RunTracker();
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var runtime = new LoopbackAiRuntime(scriptPath: Path.Combine(project.Root, "ausente", "server.py"));
        var workflows = new WorkflowService(authorizer, runtime, tracker, new SystemClock());
        var session = new EngineSession("test", workspace: authorizer, workflows: workflows, tracker: tracker);

        var projectId = Authorize(session, project.Root);
        var denied = session.Handle(Line("workflow.start", new JsonObject { ["projectId"] = "p-000000000000" })).Response;
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());

        var response = session.Handle(Line("workflow.start", new JsonObject { ["projectId"] = projectId })).Response;
        Assert.Equal("AI_RUNTIME_UNAVAILABLE", response["error"]!["code"]!.GetValue<string>());
        Assert.Equal(before, project.Fingerprint());
        await runtime.ShutdownAsync();
    }

    [Fact]
    public async Task Retomada_sem_interrupcao_e_entrada_invalida_sao_recusadas()
    {
        using var project = TempProject.Dotnet();
        var tracker = new RunTracker();
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var workflows = new WorkflowService(authorizer, new LoopbackAiRuntime(scriptPath: Path.Combine(project.Root, "ausente", "server.py")), tracker, new SystemClock());
        var session = new EngineSession("test", workspace: authorizer, workflows: workflows, tracker: tracker);
        var projectId = Authorize(session, project.Root);

        var resume = session.Handle(Line("workflow.resume", new JsonObject { ["projectId"] = projectId, ["runId"] = "run-ausente" })).Response;
        Assert.Equal("RUN_NOT_FOUND", resume["error"]!["code"]!.GetValue<string>());

        var badInput = session.Handle(Line("workflow.start", new JsonObject { ["projectId"] = projectId, ["input"] = "texto" })).Response;
        Assert.Equal(ProtocolErrorCodes.InvalidParams, badInput["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Motor_sobe_o_python_e_publica_workflow_started_no_canal_do_protocolo()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var runtime = new LoopbackAiRuntime();
        var wire = Start(project, runtime);

        try
        {
            var hello = wire.Request("engine.hello", new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "t" });
            var capabilities = hello["result"]!["capabilities"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
            Assert.Contains("workflow.start", capabilities);
            Assert.Contains("workflow.resume", capabilities);
            Assert.Contains("harness.plan", capabilities);

            var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
            var started = wire.Request("workflow.start", new JsonObject
            {
                ["projectId"] = projectId,
                ["input"] = new JsonObject { ["goal"] = "descrever o projeto" }
            });
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "workflow.start.result.schema.json"), started["result"]));
            var runId = started["result"]!["runId"]!.GetValue<string>();

            var evt = wire.Wait(m => m["kind"]?.GetValue<string>() == "event" && m["type"]?.GetValue<string>() == "workflow.started");
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "envelope.v1.schema.json"), evt));
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "workflow.started.data.schema.json"), evt["data"]));
            Assert.Equal(WorkflowService.WorkflowId, evt["data"]!["workflowId"]!.GetValue<string>());
            Assert.Equal("started", evt["data"]!["status"]!.GetValue<string>());
            Assert.Equal(runId, evt["runId"]!.GetValue<string>());
            Assert.DoesNotContain("descrever o projeto", evt.ToJsonString());

            var completed = wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runId);
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "workflow.completed.data.schema.json"), completed["data"]));
            var snapshot = wire.Request("run.snapshot", new JsonObject { ["runId"] = runId })["result"]!;
            Assert.Equal("workflow", snapshot["kind"]!.GetValue<string>());
            Assert.Equal("completed_with_limitations", snapshot["state"]!.GetValue<string>());
            var startedRetrieval = wire.Wait(m => m["type"]?.GetValue<string>() == "context.retrieval.started" && m["runId"]?.GetValue<string>() == runId);
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "context.retrieval.started.data.schema.json"), startedRetrieval["data"]));
            var codes = snapshot["result"]!["limitations"]!.AsArray().Select(item => item!["code"]!.GetValue<string>()).ToList();
            Assert.DoesNotContain("RAG_NOT_READY", codes);
            Assert.Contains("EMBEDDINGS_NOT_READY", codes);
            Assert.Contains("JUDGE_NOT_RUN", codes);
            Assert.DoesNotContain("TargetFramework", snapshot.ToJsonString());
            Assert.Contains("dotnet", snapshot["result"]!["stacks"]!.AsArray().Select(item => item!.GetValue<string>()));
            Assert.DoesNotContain("descrever o projeto", snapshot.ToJsonString());
            Assert.Equal(before, project.Fingerprint());

            var resume = wire.Request("workflow.resume", new JsonObject { ["projectId"] = projectId, ["runId"] = runId });
            Assert.Equal("WORKFLOW_NOT_INTERRUPTED", resume["error"]!["code"]!.GetValue<string>());
        }
        finally
        {
            wire.Input.Close();
            Exception? stopped = null;
            try { await wire.Running.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (Exception ex) { stopped = ex; }
            await runtime.ShutdownAsync();
            Assert.False(runtime.IsRunning);
            if (stopped is not null) throw stopped;
        }
    }

    [Fact]
    public async Task Aprovacao_pausa_o_grafo_e_a_retomada_conclui_sem_gravar()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var runtime = new LoopbackAiRuntime();
        var wire = Start(project, runtime);
        try
        {
            var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
            var started = wire.Request("workflow.start", new JsonObject
            {
                ["projectId"] = projectId,
                ["input"] = new JsonObject { ["goal"] = "alterar o harness", ["requiresApproval"] = true }
            });
            var runId = started["result"]!["runId"]!.GetValue<string>();
            var paused = wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.interrupted" && m["runId"]?.GetValue<string>() == runId);
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "workflow.interrupted.data.schema.json"), paused["data"]));
            Assert.Contains("describe", paused["data"]!["steps"]!.AsArray().Select(item => item!.GetValue<string>()));
            Assert.DoesNotContain("alterar o harness", paused.ToJsonString());

            var snapshot = wire.Request("run.snapshot", new JsonObject { ["runId"] = runId })["result"]!;
            Assert.Equal("awaiting_approval", snapshot["state"]!.GetValue<string>());

            var resume = wire.Request("workflow.resume", new JsonObject { ["projectId"] = projectId, ["runId"] = runId });
            Assert.Equal(runId, resume["result"]!["runId"]!.GetValue<string>());
            wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runId);
            var done = wire.Request("run.snapshot", new JsonObject { ["runId"] = runId })["result"]!;
            Assert.Equal("completed_with_limitations", done["state"]!.GetValue<string>());
            Assert.Equal(before, project.Fingerprint());
        }
        finally
        {
            wire.Input.Close();
            Exception? stopped = null;
            try { await wire.Running.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (Exception ex) { stopped = ex; }
            await runtime.ShutdownAsync();
            Assert.False(runtime.IsRunning);
            if (stopped is not null) throw stopped;
        }
    }

    [Fact]
    public void Fontes_do_indice_ignoram_segredo_env_e_gitignore_e_nao_reenviam_texto_igual()
    {
        using var project = TempProject.Dotnet();
        project.Write(".env", "API_KEY=abcd1234efgh5678\n");
        project.Write(".gitignore", "docs/oculto.md\n");
        project.Write("docs/oculto.md", "Texto oculto longo o bastante para indexar.");
        project.Write("docs/nota.md", "Texto visivel longo o bastante para indexar.");
        project.Write("segredo.cs", "class X { string api_key = \"abcd1234efgh5678\"; }\n");

        var selected = KnowledgeSources.Select(new LocalProjectFiles(project.Root), new Dictionary<string, string>());
        var paths = selected.Select(source => source.Path).ToList();
        Assert.Contains("docs/nota.md", paths);
        Assert.DoesNotContain(".env", paths);
        Assert.DoesNotContain("docs/oculto.md", paths);
        Assert.DoesNotContain("segredo.cs", paths);
        Assert.NotNull(selected.Single(source => source.Path == "docs/nota.md").Text);

        var again = KnowledgeSources.Select(
            new LocalProjectFiles(project.Root),
            selected.ToDictionary(source => source.Path, source => source.Hash));
        Assert.Null(again.Single(source => source.Path == "docs/nota.md").Text);
    }

    [Fact]
    public async Task Indice_de_um_projeto_nao_responde_no_outro()
    {
        using var first = TempProject.Dotnet();
        using var second = TempProject.Dotnet();
        const string token = "alfa-mariliz-91";
        first.Write("docs/nota.md", $"O identificador {token} fica somente neste projeto.\n");
        second.Write("docs/nota.md", "Este projeto fala de estoque e de catalogo.\n");
        var beforeSecond = second.Fingerprint();
        var runtime = new LoopbackAiRuntime();
        var wire = Start(first, runtime);
        try
        {
            var firstId = wire.Request("workspace.authorize", new JsonObject { ["path"] = first.Root })["result"]!["projectId"]!.GetValue<string>();
            var secondId = wire.Request("workspace.authorize", new JsonObject { ["path"] = second.Root })["result"]!["projectId"]!.GetValue<string>();

            var runA = wire.Request("workflow.start", new JsonObject
            {
                ["projectId"] = firstId,
                ["input"] = new JsonObject { ["goal"] = token }
            })["result"]!["runId"]!.GetValue<string>();
            wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runA);
            var hit = wire.Wait(m => m["type"]?.GetValue<string>() == "context.retrieved" && m["runId"]?.GetValue<string>() == runA);
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "context.retrieved.data.schema.json"), hit["data"]));
            Assert.Equal("docs/nota.md", hit["data"]!["filePath"]!.GetValue<string>());
            Assert.DoesNotContain(token, hit.ToJsonString());

            var owned = wire.Request("run.snapshot", new JsonObject { ["runId"] = runA })["result"]!;
            Assert.Contains(token, owned["result"]!["evidence"]![0]!["excerpt"]!.GetValue<string>());

            var runB = wire.Request("workflow.start", new JsonObject
            {
                ["projectId"] = secondId,
                ["input"] = new JsonObject { ["goal"] = token }
            })["result"]!["runId"]!.GetValue<string>();
            wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runB);
            var foreign = wire.Request("run.snapshot", new JsonObject { ["runId"] = runB })["result"]!;
            Assert.Empty(foreign["result"]!["evidence"]!.AsArray());
            Assert.DoesNotContain(token, foreign.ToJsonString());
            Assert.Equal(beforeSecond, second.Fingerprint());
        }
        finally
        {
            wire.Input.Close();
            Exception? stopped = null;
            try { await wire.Running.WaitAsync(TimeSpan.FromSeconds(20)); }
            catch (Exception ex) { stopped = ex; }
            await runtime.ShutdownAsync();
            Assert.False(runtime.IsRunning);
            if (stopped is not null) throw stopped;
        }
    }

    [Fact]
    public async Task Reinicio_do_runtime_retoma_o_checkpoint_sem_gravar_no_projeto()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var runtime = new LoopbackAiRuntime();
        var wire = Start(project, runtime);
        const string goal = "pedido-fase-d-7f3a";
        try
        {
            var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
            var runId = wire.Request("workflow.start", new JsonObject
            {
                ["projectId"] = projectId,
                ["input"] = new JsonObject { ["goal"] = goal, ["requiresApproval"] = true }
            })["result"]!["runId"]!.GetValue<string>();
            wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.interrupted" && m["runId"]?.GetValue<string>() == runId);

            var checkpoint = Path.Combine(project.DataDir, "projects", projectId, "execution", "checkpoint.sqlite");
            var journalPath = Path.Combine(project.DataDir, "projects", projectId, "runs", runId, "journal.json");
            Assert.Contains(goal, Encoding.Latin1.GetString(File.ReadAllBytes(checkpoint)));
            Assert.DoesNotContain(goal, File.ReadAllText(journalPath));
            Assert.False(File.Exists(Path.Combine(project.DataDir, projectId, "execution", "checkpoint.sqlite")));

            await runtime.ShutdownAsync();
            Assert.False(runtime.IsRunning);

            var resume = wire.Request("workflow.resume", new JsonObject { ["projectId"] = projectId, ["runId"] = runId });
            Assert.Equal(runId, resume["result"]!["runId"]!.GetValue<string>());
            wire.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runId);
            Assert.False(File.Exists(checkpoint));
            Assert.DoesNotContain(goal, File.ReadAllText(journalPath));
            Assert.Contains("JUDGE_NOT_RUN", File.ReadAllText(journalPath));
            Assert.Equal(before, project.Fingerprint());
        }
        finally
        {
            await Stop(wire, runtime);
        }
    }

    [Fact]
    public async Task Reinicio_do_motor_retoma_pelo_diario_sem_expor_o_caminho()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var store = new FileRunStore(project.DataDir);
        var registry = new FileWorkspaceRegistry(project.DataDir);
        var firstRuntime = new LoopbackAiRuntime();
        var first = Start(project, firstRuntime, store, registry);
        const string goal = "pedido-fase-d-7f3a";
        var projectId = "";
        var runId = "";
        try
        {
            projectId = first.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
            runId = first.Request("workflow.start", new JsonObject
            {
                ["projectId"] = projectId,
                ["input"] = new JsonObject { ["goal"] = goal, ["requiresApproval"] = true }
            })["result"]!["runId"]!.GetValue<string>();
            first.Wait(m => m["type"]?.GetValue<string>() == "workflow.interrupted" && m["runId"]?.GetValue<string>() == runId);
            Assert.Equal(RunStates.AwaitingApproval, store.LoadJournal(projectId, runId)!.State);
            Assert.True(File.Exists(Path.Combine(project.DataDir, "workspaces.json")));
            Assert.False(File.Exists(Path.Combine(project.Root, "workspaces.json")));
            await firstRuntime.ShutdownAsync();

            var secondRuntime = new LoopbackAiRuntime();
            var second = Start(project, secondRuntime, store, registry);
            try
            {
                var resumed = second.Request("workflow.resume", new JsonObject { ["projectId"] = projectId, ["runId"] = runId });
                Assert.Equal(runId, resumed["result"]!["runId"]!.GetValue<string>());
                second.Wait(m => m["type"]?.GetValue<string>() == "workflow.completed" && m["runId"]?.GetValue<string>() == runId);
                var journal = store.LoadJournal(projectId, runId)!;
                Assert.Equal(RunStates.CompletedWithLimitations, journal.State);
                Assert.Contains(journal.Evals, eval => eval.Id == "JUDGE_NOT_RUN" && eval.Status == "not_run");
                var journalText = File.ReadAllText(Path.Combine(project.DataDir, "projects", projectId, "runs", runId, "journal.json"));
                Assert.DoesNotContain(goal, journalText);
                Assert.DoesNotContain(project.Root, journalText);
                Assert.DoesNotContain(project.Root, second.Output.ToString());
                Assert.False(File.Exists(Path.Combine(project.DataDir, "projects", projectId, "execution", "checkpoint.sqlite")));
                Assert.Equal(before, project.Fingerprint());
            }
            finally
            {
                await Stop(second, secondRuntime);
            }
        }
        finally
        {
            await Stop(first, firstRuntime);
        }

        Assert.Equal(RunStates.CompletedWithLimitations, store.LoadJournal(projectId, runId)!.State);
    }

    [Fact]
    public void Fixture_de_workflow_started_respeita_o_envelope_e_o_corpo()
    {
        var evt = ContractFiles.ReadFixture("valid", "event.workflow.started.json");
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("protocol", "envelope.v1.schema.json"), evt));
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "workflow.started.data.schema.json"), evt!["data"]));
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("ai", "workflow.start.params.schema.json"),
            new JsonObject { ["projectId"] = "p-1", ["input"] = new JsonObject() }));
        Assert.True(ContractFiles.IsValid(
            ContractFiles.Schema("ai", "workflow.resume.params.schema.json"),
            new JsonObject { ["projectId"] = "p-1", ["runId"] = "run-1" }));
    }

    private static async Task Stop(Wire wire, LoopbackAiRuntime runtime)
    {
        wire.Input.Close();
        Exception? stopped = null;
        try { await wire.Running.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (Exception ex) { stopped = ex; }
        await runtime.ShutdownAsync();
        Assert.False(runtime.IsRunning);
        if (stopped is not null) throw stopped;
    }

    private static string Authorize(EngineSession session, string path) =>
        session.Handle(Line("workspace.authorize", new JsonObject { ["path"] = path })).Response["result"]!["projectId"]!.GetValue<string>();

    private static string Line(string method, JsonObject parameters) =>
        new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString();

    private static Wire Start(TempProject project, LoopbackAiRuntime runtime, FileRunStore? store = null, FileWorkspaceRegistry? registry = null)
    {
        var wire = new Wire();
        var relay = new EventRelay();
        var tracker = new RunTracker(relay.Emit);
        store ??= new FileRunStore(project.DataDir);
        registry ??= new FileWorkspaceRegistry(project.DataDir);
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), registry);
        var runner = new SystemProcessRunner();
        var configure = new ConfigureProjectService(
            authorizer, new TestFilesFactory(), runner, store, new SystemClock(), tracker);
        var workflows = new WorkflowService(authorizer, runtime, tracker, new SystemClock(), new TestFilesFactory(), runner, project.DataDir, store);
        var session = new EngineSession("test", workspace: authorizer, configure: configure, relay: relay, tracker: tracker, workflows: workflows);
        wire.Running = Task.Run(() => session.RunAsync(wire.Input, wire.Output, CancellationToken.None));
        return wire;
    }

    private sealed class QueueReader : TextReader
    {
        private readonly Channel<string?> _lines = Channel.CreateUnbounded<string?>();
        public void Send(JsonObject message) => _lines.Writer.TryWrite(message.ToJsonString());
        public new void Close() => _lines.Writer.TryWrite(null);
        public override async ValueTask<string?> ReadLineAsync(CancellationToken ct) => await _lines.Reader.ReadAsync(ct);
    }

    private sealed class Wire
    {
        public QueueReader Input { get; } = new();
        public StringWriter Output { get; } = new() { NewLine = "\n" };
        public Task Running { get; set; } = Task.CompletedTask;
        private int _id;

        public JsonObject Request(string method, JsonObject? parameters = null)
        {
            var id = "t" + Interlocked.Increment(ref _id);
            Input.Send(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() });
            return Wait(m => m["kind"]?.GetValue<string>() == "response" && m["id"]?.GetValue<string>() == id);
        }

        public JsonObject Wait(Func<JsonObject, bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                var match = Messages().FirstOrDefault(predicate);
                if (match is not null) return match;
                if (Running.IsFaulted) throw Running.Exception!.InnerException!;
                Thread.Sleep(50);
            }

            throw new TimeoutException("Mensagem esperada não chegou: " + string.Join(" | ", Messages().Select(m => m["type"]?.ToString() ?? m["kind"]?.ToString())));
        }

        private List<JsonObject> Messages()
        {
            string text;
            lock (Output) text = Output.ToString();
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => (JsonObject)JsonNode.Parse(l)!).ToList();
        }
    }
}
