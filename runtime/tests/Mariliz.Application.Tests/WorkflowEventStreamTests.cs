using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>O motor só republica o que é desta execução, uma vez e em ordem, e não deixa o runtime ditar o vocabulário da interface.</summary>
public class WorkflowEventStreamTests
{
    [Fact]
    public async Task Evento_repetido_atrasado_de_outra_execucao_ou_desconhecido_nao_chega_a_interface()
    {
        using var project = TempProject.Dotnet();
        var published = new List<(string Type, JsonObject Data)>();
        var tracker = new RunTracker((_, _, type, data) => { lock (published) published.Add((type, data)); });
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var runtime = new ScriptedRuntime(executionId =>
        [
            Event(executionId, 0, "workflow.started"),
            Event(executionId, 0, "workflow.started"),
            Event(executionId, 1, "agent.started", new JsonObject { ["agentVersion"] = "1" }, "architecture"),
            Event("ex-ffffffffffff", 2, "agent.started", new JsonObject { ["agentVersion"] = "1" }, "architecture"),
            Event(executionId, 3, "agent.started", new JsonObject { ["agentVersion"] = "1" }, "outro-agente"),
            Event(executionId, 4, "agent.progress", new JsonObject { ["activity"] = "lendo" }, "architecture"),
            Event(executionId, 2, "agent.completed", new JsonObject { ["status"] = "completed" }, "architecture"),
            Event(executionId, 5, "agent.completed", new JsonObject { ["status"] = "completed" }, "architecture"),
            Event(executionId, 5, "agent.completed", new JsonObject { ["status"] = "completed" }, "architecture")
        ]);
        var workflows = new WorkflowService(authorizer, runtime, tracker, new SystemClock());

        var run = await workflows.StartAsync(projectId, new JsonObject { ["goal"] = "descrever" }, CancellationToken.None);
        await run.Completion!;

        List<string> types;
        lock (published) types = published.Select(item => item.Type).Where(type => type is not ("run.state" or "run.completed")).ToList();
        Assert.Equal(["workflow.started", "agent.started", "agent.completed", "workflow.completed"], types);
        Assert.Equal(RunStates.CompletedWithLimitations, run.State);
    }

    [Fact]
    public async Task Pedido_fora_do_contrato_e_recusado_antes_de_abrir_execucao()
    {
        using var project = TempProject.Dotnet();
        var tracker = new RunTracker();
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var workflows = new WorkflowService(authorizer, new ScriptedRuntime(_ => []), tracker, new SystemClock());

        foreach (var input in new[]
        {
            new JsonObject(),
            new JsonObject { ["goal"] = "   " },
            new JsonObject { ["goal"] = new string('x', 4001) },
            new JsonObject { ["goal"] = "x", ["impact"] = "sim" },
            new JsonObject { ["goal"] = "x", ["projectPath"] = "C:/" }
        })
        {
            var error = await Assert.ThrowsAsync<EngineException>(() => workflows.StartAsync(projectId, input, CancellationToken.None));
            Assert.Equal("INVALID_PARAMS", error.Code);
        }
        Assert.True(tracker.TryStart(projectId, WorkflowService.Kind, out _));
    }

    private static JsonObject Event(string executionId, int seq, string type, JsonObject? data = null, string? agentId = null)
    {
        var evt = new JsonObject
        {
            ["schema"] = "ai.event.v1",
            ["eventId"] = $"{executionId}:{seq}",
            ["executionId"] = executionId,
            ["localSeq"] = seq,
            ["type"] = type,
            ["at"] = "2026-11-02T19:00:00.000+00:00",
            ["data"] = data ?? new JsonObject()
        };
        if (agentId is not null) evt["agentId"] = agentId;
        return evt;
    }

    /// <summary>Runtime falso: repete os eventos dados e conclui com o ack mínimo.</summary>
    private sealed class ScriptedRuntime(Func<string, JsonObject[]> script) : IAiRuntime
    {
        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public string Probe() => "ready";
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
        public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) => throw new NotSupportedException();

        public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            var executionId = request["executionId"]!.GetValue<string>();
            foreach (var evt in script(executionId)) onEvent(evt);
            var output = new JsonObject { ["summary"] = "", ["stacks"] = new JsonArray(), ["evidence"] = new JsonArray() };
            return Task.FromResult(new WorkflowAck(
                WorkflowService.WorkflowId, request["runId"]!.GetValue<string>(), request["projectId"]!.GetValue<string>(),
                executionId, "completed", output, [], [], null));
        }
    }
}
