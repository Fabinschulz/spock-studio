using System.Diagnostics;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>O Python cai no meio de uma execução: a execução falha com clareza, nada fica pela metade e a próxima sobe um processo novo.</summary>
public class AiRuntimeRestartTests
{
    /// <summary>Repassa tudo ao runtime de verdade e, no primeiro evento de cada início, mata o processo do Python.</summary>
    private sealed class KillsOnFirstEvent(LoopbackAiRuntime inner) : IAiRuntime
    {
        public int Kills { get; private set; }
        public bool Armed { get; set; } = true;

        public Task EnsureReadyAsync(CancellationToken ct) => inner.EnsureReadyAsync(ct);
        public string Probe() => inner.Probe();
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct) =>
            inner.IndexedHashesAsync(projectId, indexRoot, ct);
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => inner.CancelAsync(runId, projectId, executionRoot);
        public Task ShutdownAsync() => inner.ShutdownAsync();
        public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) => inner.ResumeAsync(request, onEvent, ct);

        public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct) =>
            inner.StartAsync(request, evt =>
            {
                onEvent(evt);
                if (!Armed || inner.ProcessId is not { } pid) return;
                Armed = false;
                Kills++;
                using var process = Process.GetProcessById(pid);
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }, ct);
    }

    private static (WorkflowService Service, KillsOnFirstEvent Runtime, LoopbackAiRuntime Loopback, string ProjectId, TempProject Project, FileRunStore Store) Build()
    {
        var project = TempProject.Dotnet();
        project.Write("docs/cobranca.md", "# Cobrança\n\nO módulo de cobrança recorrente fatura assinaturas todos os meses.\n");
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var loopback = new LoopbackAiRuntime();
        var runtime = new KillsOnFirstEvent(loopback);
        var store = new FileRunStore(project.DataDir);
        var service = new WorkflowService(authorizer, runtime, new RunTracker(), new SystemClock(), store,
            new WorkflowContextBuilder(authorizer, runtime, new TestFilesFactory(), new SystemProcessRunner(), project.DataDir),
            actions: new ProjectActionExecutor(authorizer, new TestFilesFactory()));
        return (service, runtime, loopback, projectId, project, store);
    }

    private static RunJournal Journal(string runId, string kind, string state, string projectId = "p-0123456789ab") => new()
    {
        RunId = runId,
        ProjectId = projectId,
        Kind = kind,
        State = state,
        StartedAt = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void Execucao_que_a_queda_deixou_em_andamento_vira_falha_e_so_ela()
    {
        using var project = new TempProject();
        var store = new FileRunStore(project.DataDir);
        store.SaveJournal(Journal("run-aaaaaaaaaaaa", WorkflowService.Kind, RunStates.Running));
        store.SaveJournal(Journal("run-bbbbbbbbbbbb", WorkflowService.Kind, RunStates.AwaitingApproval));
        store.SaveJournal(Journal("run-cccccccccccc", WorkflowService.Kind, RunStates.CompletedWithLimitations));
        store.SaveJournal(Journal("run-dddddddddddd", RunKinds.Apply, RunStates.Running));
        store.SaveJournal(Journal("run-eeeeeeeeeeee", WorkflowService.Kind, RunStates.Running, projectId: "p-ba9876543210"));
        Directory.CreateDirectory(Path.Combine(project.DataDir, "projects", "pasta com espaço"));
        var clock = new SystemClock();

        Assert.Equal(2, WorkflowRecovery.MarkInterrupted(store, clock));

        var interrupted = store.LoadJournal("p-0123456789ab", "run-aaaaaaaaaaaa")!;
        Assert.Equal(RunStates.Failed, interrupted.State);
        Assert.Equal(WorkflowRecovery.EngineRestarted, interrupted.Error);
        Assert.NotNull(interrupted.FinishedAt);
        Assert.Equal(RunStates.Failed, store.LoadJournal("p-ba9876543210", "run-eeeeeeeeeeee")!.State);

        // A pausa de aprovação continua retomável, a concluída segue concluída e a aplicação do harness não é desta rotina.
        Assert.Equal(RunStates.AwaitingApproval, store.LoadJournal("p-0123456789ab", "run-bbbbbbbbbbbb")!.State);
        Assert.Equal(RunStates.CompletedWithLimitations, store.LoadJournal("p-0123456789ab", "run-cccccccccccc")!.State);
        Assert.Equal(RunStates.Running, store.LoadJournal("p-0123456789ab", "run-dddddddddddd")!.State);

        // Uma segunda varredura não encontra mais nada.
        Assert.Equal(0, WorkflowRecovery.MarkInterrupted(store, clock));
    }

    [Fact]
    public void Execucao_interrompida_e_marcada_com_acoes_pendentes_nao_deixa_acao_aprovada()
    {
        using var project = new TempProject();
        var store = new FileRunStore(project.DataDir);
        var journal = Journal("run-aaaaaaaaaaaa", WorkflowService.Kind, RunStates.Running);
        journal.Actions.Add(new JournalAction { Id = "act-decisao-impacto", Kind = "file.write", Status = JournalAction.Approved });
        store.SaveJournal(journal);

        WorkflowRecovery.MarkInterrupted(store, new SystemClock());

        Assert.DoesNotContain(store.LoadJournal("p-0123456789ab", "run-aaaaaaaaaaaa")!.Actions, a => a.Status == JournalAction.Approved);
    }

    [Fact]
    public async Task O_motor_diz_ao_python_quem_ele_e_para_o_python_nao_ficar_orfao()
    {
        using var project = new TempProject();
        var recorded = Path.Combine(project.DataDir, "argv.txt");
        var script = Path.Combine(project.DataDir, "server.py");
        // Um servidor de mentira: grava como foi chamado e sai, o que basta para o motor desistir sem esperar a saúde.
        File.WriteAllText(script, $"import sys\nopen({System.Text.Json.JsonSerializer.Serialize(recorded)}, 'w').write(' '.join(sys.argv[1:]))\n");
        var python = PythonRuntimeLayout.Candidates(AppContext.BaseDirectory, null, null, Environment.GetEnvironmentVariable("PATH")).First();
        var runtime = new LoopbackAiRuntime(scriptPath: script, pythonPath: python);
        try
        {
            await Assert.ThrowsAsync<EngineException>(() => runtime.EnsureReadyAsync(CancellationToken.None));
        }
        finally
        {
            await runtime.ShutdownAsync();
        }

        Assert.Contains($"--parent-pid {Environment.ProcessId}", File.ReadAllText(recorded));
    }

    [Fact]
    public async Task Python_que_cai_no_meio_da_execucao_falha_com_clareza_e_a_proxima_sobe_outro_processo()
    {
        var (service, runtime, loopback, projectId, project, store) = Build();
        using var _ = project;
        try
        {
            var before = project.Fingerprint();
            var input = new JsonObject { ["goal"] = "avaliar o módulo de cobrança recorrente", ["impact"] = true, ["requiresApproval"] = true };

            var run = await service.StartAsync(projectId, input, CancellationToken.None);
            await run.Completion!;

            Assert.Equal(1, runtime.Kills);
            Assert.Equal(RunStates.Failed, run.State);
            Assert.Equal("AI_RUNTIME_UNAVAILABLE", run.Result!["error"]!["code"]!.GetValue<string>());
            var journal = store.LoadJournal(projectId, run.RunId)!;
            Assert.Equal(RunStates.Failed, journal.State);
            Assert.Equal("AI_RUNTIME_UNAVAILABLE", journal.Error);
            Assert.NotNull(journal.FinishedAt);
            Assert.DoesNotContain(journal.Actions, a => a.Status == JournalAction.Approved);
            Assert.Equal(before, project.Fingerprint());
            Assert.False(loopback.IsRunning);

            // A pessoa tenta de novo: o motor sobe um Python novo e a execução segue até a pausa de aprovação.
            var again = await service.StartAsync(projectId, input, CancellationToken.None);
            for (var i = 0; i < 400 && again.State != RunStates.AwaitingApproval; i++) await Task.Delay(25);
            Assert.Equal(RunStates.AwaitingApproval, again.State);
            Assert.True(loopback.IsRunning);
            await service.ResumeAsync(projectId, again.RunId);
            await again.Completion!;
            Assert.Equal(RunStates.CompletedWithLimitations, again.State);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
