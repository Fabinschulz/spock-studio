using System.Text.Json.Nodes;
using System.Threading.Channels;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>A mesma jornada que a interface faz, mas por mensagens do protocolo, com os eventos de progresso.</summary>
public class ProtocolJourneyTests
{
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
        private readonly StringWriter _written = new() { NewLine = "\n" };

        /// <summary>
        /// O motor escreve de outras threads enquanto o teste lê. O invólucro sincronizado trava a si mesmo em cada escrita, e
        /// <see cref="Messages"/> trava o mesmo objeto para ler: sem isso, o <c>ToString</c> do <c>StringBuilder</c> podia
        /// pegar um bloco em meio a uma escrita (<c>ArgumentOutOfRangeException</c> intermitente na CI).
        /// </summary>
        public TextWriter Output { get; }

        public Wire() => Output = TextWriter.Synchronized(_written);
        public Task Running { get; set; } = Task.CompletedTask;
        private int _id;

        public JsonObject Request(string method, JsonObject? p = null)
        {
            var id = "t" + Interlocked.Increment(ref _id);
            Input.Send(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = id, ["method"] = method, ["params"] = p ?? new JsonObject() });
            return Wait(m => m["kind"]?.GetValue<string>() == "response" && m["id"]?.GetValue<string>() == id);
        }

        public List<JsonObject> Messages()
        {
            string text;
            lock (Output) text = _written.ToString();
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => (JsonObject)JsonNode.Parse(l)!).ToList();
        }

        public JsonObject Wait(Func<JsonObject, bool> predicate, int seconds = 90)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (DateTime.UtcNow < deadline)
            {
                var match = Messages().FirstOrDefault(predicate);
                if (match is not null) return match;
                Thread.Sleep(50);
            }
            throw new TimeoutException("Mensagem esperada não chegou.");
        }
    }

    private static Wire Start(TempProject project)
    {
        var wire = new Wire();
        var relay = new EventRelay();
        var tracker = new RunTracker(relay.Emit);
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var store = new FileRunStore(project.DataDir);
        var configure = new ConfigureProjectService(authorizer, new TestFilesFactory(), new SystemProcessRunner(), store, new SystemClock(), tracker);
        var session = new EngineSession("test", workspace: authorizer, configure: configure, relay: relay, tracker: tracker, runs: new RunQueries(tracker, store));
        wire.Running = Task.Run(() => session.RunAsync(wire.Input, wire.Output, CancellationToken.None));
        return wire;
    }

    [Fact]
    public async Task Jornada_completa_pelo_protocolo_com_eventos_de_progresso_e_snapshot()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);
        var wire = Start(project);

        var hello = wire.Request("engine.hello", new JsonObject { ["protocol"] = new JsonArray(1), ["hostVersion"] = "t" });
        var capabilities = hello["result"]!["capabilities"]!.AsArray().Select(c => c!.GetValue<string>()).ToList();
        Assert.Contains("harness.plan", capabilities);
        Assert.Contains("harness.apply", capabilities);

        // O caminho só entra por workspace.authorize; sem ele o projeto é desconhecido.
        var denied = wire.Request("project.discover", new JsonObject { ["projectId"] = "p-000000000000" });
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());

        var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
        var discovery = wire.Request("project.discover", new JsonObject { ["projectId"] = projectId })["result"]!;
        Assert.Equal("dotnet", discovery["stacks"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("installed", discovery["specification"]!["state"]!.GetValue<string>());

        var plan = wire.Request("harness.plan", new JsonObject { ["projectId"] = projectId, ["aiEnvironment"] = "claude-code" })["result"]!;
        var changeIds = new JsonArray(plan["changes"]!.AsArray()
            .Where(c => c!["action"]!.GetValue<string>() is "create" or "update").Select(c => (JsonNode)c!["id"]!.GetValue<string>()).ToArray());
        Assert.NotEmpty(changeIds);
        Assert.Equal("create", plan["changes"]![0]!["action"]!.GetValue<string>().Replace("preserve", "create"));

        var apply = wire.Request("harness.apply", new JsonObject
        {
            ["projectId"] = projectId, ["planId"] = plan["planId"]!.GetValue<string>(),
            ["approvedChangeIds"] = changeIds, ["approvedCommandIds"] = new JsonArray()
        });
        var runId = apply["result"]!["runId"]!.GetValue<string>();

        var completed = wire.Wait(m => m["kind"]?.GetValue<string>() == "event" && m["type"]?.GetValue<string>() == "run.completed" && m["runId"]?.GetValue<string>() == runId);
        Assert.Equal("completed", completed["data"]!["state"]!.GetValue<string>());

        var events = wire.Messages().Where(m => m["kind"]?.GetValue<string>() == "event" && m["runId"]?.GetValue<string>() == runId).ToList();
        Assert.Contains(events, e => e["type"]!.GetValue<string>() == "step.started" && e["data"]!["id"]!.GetValue<string>() == "write");
        Assert.Contains(events, e => e["type"]!.GetValue<string>() == "file.applied");
        var seqs = events.Select(e => e["seq"]!.GetValue<int>()).ToList();
        Assert.Equal(seqs.Order().ToList(), seqs.Distinct().Order().ToList());

        var snapshot = wire.Request("run.snapshot", new JsonObject { ["runId"] = runId })["result"]!;
        Assert.Equal("completed", snapshot["state"]!.GetValue<string>());
        Assert.Equal("completed", snapshot["result"]!["outcome"]!.GetValue<string>());
        Assert.Equal("passed", snapshot["result"]!["validation"]!["results"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "EV-SPEC-001")!["status"]!.GetValue<string>());

        var runs = wire.Request("harness.runs", new JsonObject { ["projectId"] = projectId })["result"]!["runs"]!.AsArray();
        Assert.Equal(runId, runs[0]!["runId"]!.GetValue<string>());

        wire.Request("engine.shutdown");
        await wire.Running;
    }

    [Fact]
    public async Task Parametros_invalidos_e_erros_do_dominio_viram_erros_tipados_sem_detalhes_internos()
    {
        using var project = TempProject.Dotnet();
        var wire = Start(project);
        var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();

        Assert.Equal("INVALID_PARAMS", wire.Request("harness.plan", new JsonObject())["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", wire.Request("harness.plan", new JsonObject { ["projectId"] = projectId, ["aiEnvironment"] = "vim" })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("PLAN_UNKNOWN", wire.Request("harness.apply", new JsonObject { ["projectId"] = projectId, ["planId"] = "plan-x" })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("RUN_NOT_FOUND", wire.Request("run.snapshot", new JsonObject { ["runId"] = "run-x" })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", wire.Request("harness.apply", new JsonObject { ["projectId"] = projectId, ["planId"] = "p", ["resolutions"] = new JsonObject { ["c"] = "talvez" } })["error"]!["code"]!.GetValue<string>());

        wire.Request("engine.shutdown");
        await wire.Running;
    }

    [Fact]
    public async Task Encerrar_o_motor_no_meio_de_uma_aplicacao_desfaz_o_que_foi_feito()
    {
        using var project = TempProject.Dotnet();
        var before = project.Fingerprint();
        var started = new TaskCompletionSource();
        var runner = new InterceptingRunner(async (spec, ct) =>
        {
            if (spec.Executable != "npx") return null;
            SpecificationFixture.Install(project);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return null;
        });

        var wire = new Wire();
        var relay = new EventRelay();
        var tracker = new RunTracker(relay.Emit);
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var store = new FileRunStore(project.DataDir);
        var configure = new ConfigureProjectService(authorizer, new TestFilesFactory(), runner, store, new SystemClock(), tracker);
        var session = new EngineSession("test", workspace: authorizer, configure: configure, relay: relay, tracker: tracker, runs: new RunQueries(tracker, store));
        wire.Running = Task.Run(() => session.RunAsync(wire.Input, wire.Output, CancellationToken.None));

        var projectId = wire.Request("workspace.authorize", new JsonObject { ["path"] = project.Root })["result"]!["projectId"]!.GetValue<string>();
        var plan = wire.Request("harness.plan", new JsonObject { ["projectId"] = projectId, ["aiEnvironment"] = "claude-code" })["result"]!;
        wire.Request("harness.apply", new JsonObject
        {
            ["projectId"] = projectId, ["planId"] = plan["planId"]!.GetValue<string>(),
            ["approvedChangeIds"] = new JsonArray(), ["approvedCommandIds"] = new JsonArray("specification-install")
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        // A interface some (stdin fecha): a execução em andamento é cancelada e desfeita antes de o motor sair.
        wire.Input.Close();
        await wire.Running.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(before, project.Fingerprint());
    }
}
