using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>Uma execução sempre termina, a aprovação humana não tem prazo e uma retomada imediata encontra a aprovação aberta.</summary>
public class WorkflowRobustnessTests
{
    [Fact]
    public async Task Aprovacao_mais_lenta_que_o_prazo_do_runtime_ainda_conclui()
    {
        using var project = TempProject.Dotnet();
        var (workflows, projectId, _) = Build(project, new PausingRuntime(), TimeSpan.FromMilliseconds(150));

        var run = await workflows.StartAsync(projectId, new JsonObject { ["goal"] = "x", ["requiresApproval"] = true }, CancellationToken.None);
        await WaitUntil(() => run.State == RunStates.AwaitingApproval);
        await Task.Delay(600);
        await workflows.ResumeAsync(projectId, run.RunId);
        await run.Completion!;

        Assert.Equal(RunStates.CompletedWithLimitations, run.State);
    }

    [Fact]
    public async Task Retomada_disparada_pelo_proprio_evento_de_aprovacao_encontra_a_aprovacao()
    {
        using var project = TempProject.Dotnet();
        WorkflowService? workflows = null;
        string projectId = "";
        Exception? refused = null;
        var tracker = new RunTracker((runId, _, type, _) =>
        {
            if (type != "approval.required") return;
            try { workflows!.ResumeAsync(projectId, runId).GetAwaiter().GetResult(); }
            catch (Exception ex) { refused = ex; }
        });
        (workflows, projectId) = Build(project, new PausingRuntime(), tracker);

        var run = await workflows.StartAsync(projectId, new JsonObject { ["goal"] = "x", ["requiresApproval"] = true }, CancellationToken.None);
        await run.Completion!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(refused);
        Assert.Equal(RunStates.CompletedWithLimitations, run.State);
    }

    [Theory]
    [InlineData("json", "AI_RUNTIME_INVALID_RESPONSE")]
    [InlineData("argument", "WORKFLOW_FAILED")]
    public async Task Erro_imprevisto_do_runtime_termina_a_execucao_e_libera_o_projeto(string failure, string code)
    {
        using var project = TempProject.Dotnet();
        var (workflows, projectId, tracker) = Build(project, new FailingRuntime(failure), TimeSpan.FromSeconds(5));

        var run = await workflows.StartAsync(projectId, new JsonObject { ["goal"] = "x" }, CancellationToken.None);
        await run.Completion!.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(RunStates.Failed, run.State);
        Assert.Equal(code, run.Result!["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain("segredo", run.Result.ToJsonString());
        Assert.True(tracker.TryStart(projectId, WorkflowService.Kind, out _));
    }

    [Fact]
    public async Task Guarda_da_execucao_conclui_como_falha_o_que_escapar_do_servico()
    {
        var run = new RunHandle("run-1", "p-1", "validate", (_, _, _, _) => { });
        run.Start(() => throw new IOException("C:/segredo/caminho"));
        await run.Completion!;

        Assert.Equal(RunStates.Failed, run.State);
        Assert.Equal("INTERNAL_ERROR", run.Result!["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain("segredo", run.Result.ToJsonString());
    }

    private static (WorkflowService, string, RunTracker) Build(TempProject project, IAiRuntime runtime, TimeSpan deadline)
    {
        var tracker = new RunTracker();
        var (workflows, projectId) = Build(project, runtime, tracker, deadline);
        return (workflows, projectId, tracker);
    }

    private static (WorkflowService, string) Build(TempProject project, IAiRuntime runtime, RunTracker tracker, TimeSpan? deadline = null)
    {
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var workflows = new WorkflowService(authorizer, runtime, tracker, new SystemClock(), store: new FileRunStore(project.DataDir))
        {
            Deadline = deadline ?? TimeSpan.FromSeconds(5)
        };
        return (workflows, projectId);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException();
            await Task.Delay(20);
        }
    }

    private static WorkflowAck Ack(JsonObject request, string status, string? interruptId = null) => new(
        WorkflowService.WorkflowId, request["runId"]!.GetValue<string>(), request["projectId"]!.GetValue<string>(),
        request["executionId"]!.GetValue<string>(), status,
        new JsonObject { ["summary"] = "", ["stacks"] = new JsonArray(), ["evidence"] = new JsonArray() }, [], [], interruptId);

    /// <summary>Pausa no início e conclui na retomada, respeitando o cancelamento como o runtime real.</summary>
    private sealed class PausingRuntime : FakeRuntime
    {
        public override Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
            Task.FromResult(Ack(request, "interrupted", "int-0123456789ab"));

        public override async Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            return Ack(request, "completed");
        }
    }

    private sealed class FailingRuntime(string failure) : FakeRuntime
    {
        public override Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
            failure == "json"
                ? throw new JsonException("linha quebrada com C:/segredo")
                : throw new ArgumentException("C:/segredo/caminho");
    }

    private abstract class FakeRuntime : IAiRuntime
    {
        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public string Probe() => "ready";
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
        public abstract Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct);
        public virtual Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
