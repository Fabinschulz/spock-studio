using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>O motor decide se a execução usa modelo: só quando pedido, só com modelo configurado, e o uso fica no resultado.</summary>
public class WorkflowModelTests
{
    private sealed class FakeAccess(bool configured = true) : IModelAccess
    {
        public bool Configured => configured;
        public List<string> Opened { get; } = [];
        public List<string> Closed { get; } = [];
        public int OpenBudget;

        public ModelGrant Open(string executionId, int tokenBudget)
        {
            Opened.Add(executionId);
            OpenBudget = tokenBudget;
            return new ModelGrant("http://127.0.0.1:49152/v1/model", new string('t', 43), tokenBudget);
        }

        public ModelUsage Close(string executionId)
        {
            Closed.Add(executionId);
            return new ModelUsage(2, 900, 300, 0);
        }

        public IReadOnlyList<ModelCallRecord> Calls(string executionId) => [];
    }

    private sealed class CapturingRuntime : IAiRuntime
    {
        public JsonObject? Started;
        public bool FailStart;

        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public string Probe() => "ready";
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;
        public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) => throw new NotSupportedException();

        public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            Started = (JsonObject)request.DeepClone();
            if (FailStart) throw new InvalidOperationException("caiu");
            var output = new JsonObject { ["summary"] = "", ["stacks"] = new JsonArray(), ["evidence"] = new JsonArray() };
            return Task.FromResult(new WorkflowAck(
                WorkflowService.WorkflowId, request["runId"]!.GetValue<string>(), request["projectId"]!.GetValue<string>(),
                request["executionId"]!.GetValue<string>(), "completed", output, [], [], null));
        }
    }

    private static (WorkflowService Service, CapturingRuntime Runtime, string ProjectId, FileRunStore Store, TempProject Project) Build(IModelAccess? access)
    {
        var project = TempProject.Dotnet();
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var runtime = new CapturingRuntime();
        var store = new FileRunStore(project.DataDir);
        var service = new WorkflowService(authorizer, runtime, new RunTracker(), new SystemClock(), store, WorkflowContextBuilder.WithoutProject, access);
        return (service, runtime, projectId, store, project);
    }

    [Fact]
    public async Task Sem_pedir_modelo_a_execucao_segue_sem_proxy_sem_tokens_e_sem_uso()
    {
        var access = new FakeAccess();
        var (service, runtime, projectId, _, project) = Build(access);
        using var _ = project;

        var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever" }, CancellationToken.None);
        await run.Completion!;

        Assert.Empty(access.Opened);
        var start = runtime.Started!;
        Assert.False(start["policy"]!["modelAllowed"]!.GetValue<bool>());
        Assert.Null(start["broker"]);
        Assert.Equal(0, start["limits"]!["maxTokens"]!.GetValue<int>());
        Assert.Null(run.Result!["usage"]);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "ai.workflow.start.v2.schema.json"), start));
    }

    [Fact]
    public async Task Pedir_modelo_abre_o_proxy_com_o_teto_da_D4_e_a_mensagem_continua_no_contrato()
    {
        var access = new FakeAccess();
        var (service, runtime, projectId, _, project) = Build(access);
        using var _ = project;

        var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = true }, CancellationToken.None);
        await run.Completion!;

        var start = runtime.Started!;
        Assert.Equal(WorkflowRequests.ModelTokenBudget, access.OpenBudget);
        Assert.Equal(8000, start["limits"]!["maxTokens"]!.GetValue<int>());
        Assert.Equal(WorkflowRequests.ModelMilliseconds, start["limits"]!["maxMilliseconds"]!.GetValue<int>());
        Assert.True(start["policy"]!["modelAllowed"]!.GetValue<bool>());
        Assert.Equal("http://127.0.0.1:49152/v1/model", start["broker"]!["url"]!.GetValue<string>());
        // `useModel` é decisão do motor: o runtime nunca a vê no pedido, só como política e proxy.
        Assert.Null(start["input"]!["useModel"]);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "ai.workflow.start.v2.schema.json"), start));
    }

    [Fact]
    public async Task O_proxy_fecha_ao_fim_da_conversa_e_o_uso_contado_pelo_motor_vai_ao_resultado_e_ao_diario()
    {
        var access = new FakeAccess();
        var (service, _, projectId, store, project) = Build(access);
        using var _ = project;

        var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = true }, CancellationToken.None);
        await run.Completion!;

        Assert.Equal(access.Opened, access.Closed);
        var usage = run.Result!["usage"]!;
        Assert.Equal(2, usage["modelCalls"]!.GetValue<int>());
        Assert.Equal(900, usage["inputTokens"]!.GetValue<int>());
        Assert.Equal(300, usage["outputTokens"]!.GetValue<int>());
        var journal = store.LoadJournal(projectId, run.RunId)!;
        Assert.Equal(new[] { 2, 900, 300 }, new[] { journal.ModelUsage!.Calls, journal.ModelUsage.InputTokens, journal.ModelUsage.OutputTokens });
    }

    [Fact]
    public async Task O_proxy_fecha_tambem_quando_o_runtime_falha()
    {
        var access = new FakeAccess();
        var (service, runtime, projectId, _, project) = Build(access);
        using var _ = project;
        runtime.FailStart = true;

        var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = true }, CancellationToken.None);
        await run.Completion!;

        Assert.Equal(RunStates.Failed, run.State);
        Assert.Equal(access.Opened, access.Closed);
    }

    [Fact]
    public async Task Pedir_modelo_sem_modelo_configurado_e_recusado_antes_de_abrir_a_execucao()
    {
        foreach (var access in new IModelAccess?[] { null, new FakeAccess(configured: false) })
        {
            var (service, runtime, projectId, _, project) = Build(access);
            using var _ = project;

            var error = await Assert.ThrowsAsync<EngineException>(() =>
                service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = true }, CancellationToken.None));

            Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
            Assert.Null(runtime.Started);
        }
    }

    [Fact]
    public async Task useModel_precisa_ser_booleano()
    {
        var (service, _, projectId, _, project) = Build(new FakeAccess());
        using var _ = project;

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = "sim" }, CancellationToken.None));

        Assert.Equal("INVALID_PARAMS", error.Code);
    }
}
