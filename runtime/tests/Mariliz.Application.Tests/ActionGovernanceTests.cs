using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// Fase 7b: o agente só propõe. Estas provas tentam o que um agente (ou um cliente, ou um diário adulterado) faria para
/// executar sem aprovação, e conferem que nada é gravado no projeto.
/// </summary>
public class ActionGovernanceTests
{
    private const string Chunk = "docs/nota.md#1";
    private const string Interrupt = "int-0123456789ab";

    /// <summary>Runtime que pausa propondo o que o teste mandar e, retomado, conclui (ou falha).</summary>
    private sealed class ProposingRuntime(Func<string, JsonObject[]> proposals) : IAiRuntime
    {
        public JsonObject? Started;
        public JsonObject? Resumed;
        public bool FailOnResume;

        public Task EnsureReadyAsync(CancellationToken ct) => Task.CompletedTask;
        public string Probe() => "ready";
        public Task<IReadOnlyDictionary<string, string>> IndexedHashesAsync(string projectId, string indexRoot, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task CancelAsync(string runId, string? projectId = null, string? executionRoot = null) => Task.CompletedTask;
        public Task ShutdownAsync() => Task.CompletedTask;

        public Task<WorkflowAck> StartAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            Started = (JsonObject)request.DeepClone();
            var executionId = request["executionId"]!.GetValue<string>();
            return Task.FromResult(Ack(request, "interrupted", Interrupt, proposals(executionId)));
        }

        public Task<WorkflowAck> ResumeAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            Resumed = (JsonObject)request.DeepClone();
            return Task.FromResult(Ack(request, FailOnResume ? "failed" : "completed", null, []));
        }

        private static WorkflowAck Ack(JsonObject request, string status, string? interrupt, JsonObject[] actions)
        {
            var output = new JsonObject
            {
                ["summary"] = "",
                ["stacks"] = new JsonArray(),
                ["evidence"] = new JsonArray(new JsonObject { ["chunkId"] = Chunk, ["filePath"] = "docs/nota.md", ["hash"] = new string('b', 64), ["score"] = 1.0, ["excerpt"] = "x" }),
                ["impact"] = new JsonObject { ["schema"] = "engineering.impact.v1", ["areas"] = new JsonArray("servico", "interface"), ["risks"] = new JsonArray(), ["evidence"] = new JsonArray(Chunk) }
            };
            return new WorkflowAck(WorkflowService.WorkflowId, request["runId"]!.GetValue<string>(), request["projectId"]!.GetValue<string>(),
                request["executionId"]!.GetValue<string>(), status, output, [], status == "failed" ? [new WorkflowLimitation("PLAN_INVALID", "x")] : [], interrupt, actions);
        }
    }

    private static JsonObject Proposal(string executionId, string? target = null, string kind = "file.write", string id = "act-decisao-impacto", bool requiresApproval = true) => new()
    {
        ["id"] = id,
        ["kind"] = kind,
        ["summary"] = "TEXTO-DO-AGENTE: registrar a decisão",
        ["target"] = target ?? $".mariliz/decisions/{executionId}-impacto.md",
        ["evidence"] = new JsonArray(Chunk),
        ["requiresApproval"] = requiresApproval
    };

    private sealed class Rig : IDisposable
    {
        public TempProject Project { get; } = TempProject.Dotnet();
        public FileRunStore Store { get; }
        public WorkspaceAuthorizer Authorizer { get; } = new(new LocalWorkspaceFileSystem());
        public string ProjectId { get; }
        public SpyExecutor Executor { get; }

        public Rig()
        {
            Store = new FileRunStore(Project.DataDir);
            ProjectId = Authorizer.Authorize(Project.Root).Project!.ProjectId;
            Executor = new SpyExecutor(new ProjectActionExecutor(Authorizer, new TestFilesFactory()));
        }

        public (WorkflowService Service, RunTracker Tracker) NewEngine(ProposingRuntime runtime)
        {
            var tracker = new RunTracker();
            return (new WorkflowService(Authorizer, runtime, tracker, new SystemClock(), Store, WorkflowContextBuilder.WithoutProject, actions: Executor), tracker);
        }

        public void Dispose() => Project.Dispose();
    }

    private sealed class SpyExecutor(IActionExecutor inner) : IActionExecutor
    {
        public List<string> Executed { get; } = [];

        public ActionOutcome Execute(string projectId, JournalAction action)
        {
            Executed.Add(action.Id);
            return inner.Execute(projectId, action);
        }
    }

    private static JsonObject Pausing() => new() { ["goal"] = "avaliar o módulo", ["impact"] = true, ["requiresApproval"] = true };

    private static async Task<(RunHandle Run, string ApprovalId, string ExecutionId)> PauseAsync(WorkflowService service, string projectId)
    {
        var run = await service.StartAsync(projectId, Pausing(), CancellationToken.None);
        var executionId = await WaitFor(() => run.Snapshot()["events"]!.AsArray().Any(e => e!["type"]!.GetValue<string>() == "approval.required") ? true : null) ? run.RunId : "";
        var approval = run.Snapshot()["events"]!.AsArray().Single(e => e!["type"]!.GetValue<string>() == "approval.required")!["data"]!;
        return (run, approval["approvalId"]!.GetValue<string>(), approval["executionId"]!.GetValue<string>());
    }

    private static async Task<bool> WaitFor(Func<bool?> condition)
    {
        for (var i = 0; i < 200; i++)
        {
            if (condition() == true) return true;
            await Task.Delay(25);
        }
        throw new TimeoutException("A condição não aconteceu a tempo.");
    }

    private static string DecisionPath(TempProject project, string executionId) => Path.Combine(project.Root, ".mariliz", "decisions", executionId + "-impacto.md");

    [Fact]
    public async Task A_politica_do_motor_manda_so_file_write_e_so_em_execucao_que_pausa_e_pede_impacto()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]);
        var (service, _) = rig.NewEngine(runtime);

        await service.StartAsync(rig.ProjectId, Pausing(), CancellationToken.None);
        await WaitFor(() => runtime.Started is not null);
        Assert.Equal(["file.write"], runtime.Started!["policy"]!["allowedActions"]!.AsArray().Select(a => a!.GetValue<string>()));

        foreach (var input in new[]
        {
            new JsonObject { ["goal"] = "x", ["impact"] = true },
            new JsonObject { ["goal"] = "x", ["requiresApproval"] = true },
            new JsonObject { ["goal"] = "x" }
        })
        {
            Assert.Empty(ActionGovernance.AllowedKinds(input));
        }
    }

    [Fact]
    public async Task Proposta_fora_da_politica_do_formato_ou_da_recuperacao_e_descartada_antes_de_qualquer_pessoa_ver()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex =>
        [
            Proposal(ex, kind: "command.run", id: "act-comando"),
            Proposal(ex, target: "src/Program.cs", id: "act-fora"),
            Proposal(ex, target: ".mariliz/decisions/../../src/x.md", id: "act-sobe"),
            Proposal(ex, target: ".mariliz/decisions/ex-ffffffffffff-impacto.md", id: "act-outra"),
            new JsonObject { ["id"] = "act-conteudo", ["kind"] = "file.write", ["summary"] = "x", ["target"] = $".mariliz/decisions/{ex}-impacto.md", ["content"] = "# escrito pelo agente", ["requiresApproval"] = true },
            new JsonObject { ["id"] = "act-cita", ["kind"] = "file.write", ["summary"] = "x", ["target"] = $".mariliz/decisions/{ex}-impacto.md", ["evidence"] = new JsonArray("docs/inventado.md#9"), ["requiresApproval"] = true },
            Proposal(ex, id: "ACT-maiuscula")
        ]);
        var (service, _) = rig.NewEngine(runtime);

        var (run, _, _) = await PauseAsync(service, rig.ProjectId);

        var approval = run.Snapshot()["events"]!.AsArray().Single(e => e!["type"]!.GetValue<string>() == "approval.required")!["data"]!;
        Assert.Empty(approval["actions"]!.AsArray());
        Assert.Empty(rig.Store.LoadJournal(rig.ProjectId, run.RunId)!.Actions);
    }

    [Fact]
    public async Task A_aprovacao_mostra_as_acoes_e_e_ligada_ao_hash_delas()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));

        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        var data = run.Snapshot()["events"]!.AsArray().Single(e => e!["type"]!.GetValue<string>() == "approval.required")!["data"]!;
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "approval.required.data.schema.json"), data));
        var action = data["actions"]!.AsArray().Single()!;
        Assert.Equal($".mariliz/decisions/{executionId}-impacto.md", action["target"]!.GetValue<string>());
        // A tela só recebe o que a política mostra (id, tipo, resumo e alvo), nunca o conteúdo.
        Assert.Null(action["content"]);

        var journal = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!;
        Assert.Equal(approvalId, ActionGovernance.ApprovalIdFor(run.RunId, executionId, Interrupt, ActionGovernance.HashOf(journal.Actions)));
        Assert.Equal(JournalAction.Proposed, journal.Actions.Single().Status);
        Assert.False(File.Exists(DecisionPath(rig.Project, executionId)), "propor não grava");
    }

    [Fact]
    public async Task Sem_aprovar_a_acao_nada_e_gravado_e_o_runtime_recebe_a_lista_de_aprovadas_vazia()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]);
        var (service, _) = rig.NewEngine(runtime);
        var (run, _, executionId) = await PauseAsync(service, rig.ProjectId);

        await (await service.ResumeAsync(rig.ProjectId, run.RunId)).Completion!;

        Assert.False(File.Exists(DecisionPath(rig.Project, executionId)));
        Assert.Empty(rig.Executor.Executed);
        Assert.Empty(runtime.Resumed!["decision"]!["actionIds"]!.AsArray());
        Assert.Equal(JournalAction.Skipped, run.Result!["actions"]!.AsArray().Single()!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Aprovar_com_o_identificador_certo_executa_so_o_que_o_motor_escreveu()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]);
        var (service, _) = rig.NewEngine(runtime);
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        await (await service.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"]))).Completion!;

        var path = DecisionPath(rig.Project, executionId);
        Assert.True(File.Exists(path));
        var written = File.ReadAllText(path);
        Assert.Contains("# Decisão de arquitetura", written);
        Assert.Contains("- Serviço", written);
        Assert.Contains("`docs/nota.md`", written);
        // O texto do agente nunca é o conteúdo do arquivo.
        Assert.DoesNotContain("TEXTO-DO-AGENTE", written);
        Assert.Equal(["act-decisao-impacto"], runtime.Resumed!["decision"]!["actionIds"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Equal(JournalAction.Executed, run.Result!["actions"]!.AsArray().Single()!["status"]!.GetValue<string>());
        Assert.Equal(JournalAction.Executed, rig.Store.LoadJournal(rig.ProjectId, run.RunId)!.Actions.Single().Status);
    }

    [Fact]
    public async Task Aprovacao_de_outra_execucao_ou_sem_identificador_nao_vale_e_a_pausa_continua()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]);
        var (service, _) = rig.NewEngine(runtime);
        var (runA, approvalA, executionA) = await PauseAsync(service, rig.ProjectId);
        runtime.Started = null;
        // Outra execução do mesmo projeto, depois que a primeira saiu do caminho.
        runA.Cancellation.Cancel();
        await runA.Completion!;
        var (runB, approvalB, executionB) = await PauseAsync(service, rig.ProjectId);
        Assert.NotEqual(approvalA, approvalB);

        var other = await Assert.ThrowsAsync<EngineException>(() => service.ResumeAsync(rig.ProjectId, runB.RunId, new ResumeDecision(approvalA, ["act-decisao-impacto"])));
        var missing = await Assert.ThrowsAsync<EngineException>(() => service.ResumeAsync(rig.ProjectId, runB.RunId, new ResumeDecision(null, ["act-decisao-impacto"])));
        var invented = await Assert.ThrowsAsync<EngineException>(() => service.ResumeAsync(rig.ProjectId, runB.RunId, new ResumeDecision(approvalB, ["act-inventada"])));

        Assert.Equal("APPROVAL_MISMATCH", other.Code);
        Assert.Equal("APPROVAL_MISMATCH", missing.Code);
        Assert.Equal("APPROVAL_ACTION_UNKNOWN", invented.Code);
        Assert.False(File.Exists(DecisionPath(rig.Project, executionB)));
        Assert.False(File.Exists(DecisionPath(rig.Project, executionA)));
        Assert.Empty(rig.Executor.Executed);
        Assert.Equal(RunStates.AwaitingApproval, runB.State);

        // A aprovação certa ainda funciona: as recusas não gastaram a pausa.
        await (await service.ResumeAsync(rig.ProjectId, runB.RunId, new ResumeDecision(approvalB, ["act-decisao-impacto"]))).Completion!;
        Assert.True(File.Exists(DecisionPath(rig.Project, executionB)));
    }

    [Fact]
    public async Task Diario_alterado_depois_da_aprovacao_aberta_nao_retoma_nem_grava_mesmo_depois_de_um_reinicio()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        // O diário em disco é editado: o alvo da ação passa a ser outro arquivo do projeto.
        var tampered = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!;
        tampered.Actions[0].Target = ".mariliz/decisions/ex-ffffffffffff-impacto.md";
        rig.Store.SaveJournal(tampered);

        // O motor reinicia: uma instância nova, sem a execução na memória, retoma pelo diário.
        var (restarted, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var refused = await Assert.ThrowsAsync<EngineException>(() => restarted.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"])));

        Assert.Equal("APPROVAL_HASH_MISMATCH", refused.Code);
        Assert.False(File.Exists(DecisionPath(rig.Project, executionId)));
        Assert.False(File.Exists(Path.Combine(rig.Project.Root, ".mariliz", "decisions", "ex-ffffffffffff-impacto.md")));
        Assert.Empty(rig.Executor.Executed);
        // E o conteúdo também: trocar o texto aprovado muda o hash.
        var contentTampered = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!;
        contentTampered.Actions[0].Target = $".mariliz/decisions/{executionId}-impacto.md";
        contentTampered.Actions[0].ContentSha256 = new string('0', 64);
        rig.Store.SaveJournal(contentTampered);
        var again = await Assert.ThrowsAsync<EngineException>(() => restarted.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"])));
        Assert.Equal("APPROVAL_HASH_MISMATCH", again.Code);
    }

    [Fact]
    public async Task Depois_de_um_reinicio_a_aprovacao_correta_ainda_executa()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        var runtime = new ProposingRuntime(_ => []);
        var (restarted, _) = rig.NewEngine(runtime);
        var resumed = await restarted.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"]));
        await resumed.Completion!;

        Assert.True(File.Exists(DecisionPath(rig.Project, executionId)));
        Assert.Equal(["act-decisao-impacto"], runtime.Resumed!["decision"]!["actionIds"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Single(rig.Executor.Executed);
    }

    [Fact]
    public async Task Retomada_repetida_nao_executa_de_novo()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);
        var decision = new ResumeDecision(approvalId, ["act-decisao-impacto"]);
        await (await service.ResumeAsync(rig.ProjectId, run.RunId, decision)).Completion!;
        var first = File.ReadAllBytes(DecisionPath(rig.Project, executionId));

        await Assert.ThrowsAsync<EngineException>(() => service.ResumeAsync(rig.ProjectId, run.RunId, decision));

        Assert.Single(rig.Executor.Executed);
        Assert.Equal(first, File.ReadAllBytes(DecisionPath(rig.Project, executionId)));
    }

    [Fact]
    public async Task Ainda_que_a_execucao_rode_de_novo_o_que_ja_foi_executado_nao_e_repetido()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, approvalId, _) = await PauseAsync(service, rig.ProjectId);
        await (await service.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"]))).Completion!;

        var journal = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!;
        ActionGovernance.ExecuteApproved(journal, rig.ProjectId, rig.Executor, () => { });
        ActionGovernance.ExecuteApproved(journal, rig.ProjectId, rig.Executor, () => { });

        Assert.Single(rig.Executor.Executed);
    }

    [Fact]
    public async Task Se_a_execucao_falha_depois_da_aprovacao_nada_aprovado_e_executado()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]) { FailOnResume = true };
        var (service, _) = rig.NewEngine(runtime);
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        await (await service.ResumeAsync(rig.ProjectId, run.RunId, new ResumeDecision(approvalId, ["act-decisao-impacto"]))).Completion!;

        Assert.Equal(RunStates.Failed, run.State);
        Assert.False(File.Exists(DecisionPath(rig.Project, executionId)));
        Assert.Empty(rig.Executor.Executed);
        Assert.Equal(JournalAction.Skipped, run.Result?["actions"]?.AsArray().SingleOrDefault()?["status"]?.GetValue<string>() ?? JournalAction.Skipped);
    }

    [Fact]
    public async Task O_executor_nunca_sobrescreve_arquivo_diferente_e_repetir_o_mesmo_conteudo_nao_muda_nada()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, _, executionId) = await PauseAsync(service, rig.ProjectId);
        var action = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!.Actions.Single();
        var executor = new ProjectActionExecutor(rig.Authorizer, new TestFilesFactory());
        var path = DecisionPath(rig.Project, executionId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, "texto que a pessoa escreveu");
        Assert.Equal(JournalAction.Conflict, executor.Execute(rig.ProjectId, action).Status);
        Assert.Equal("texto que a pessoa escreveu", File.ReadAllText(path));

        File.Delete(path);
        Assert.Equal(JournalAction.Executed, executor.Execute(rig.ProjectId, action).Status);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(JournalAction.Executed, executor.Execute(rig.ProjectId, action).Status);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task O_executor_recusa_alvo_fora_da_pasta_e_conteudo_que_nao_bate_com_o_hash()
    {
        using var rig = new Rig();
        var (service, _) = rig.NewEngine(new ProposingRuntime(ex => [Proposal(ex)]));
        var (run, _, _) = await PauseAsync(service, rig.ProjectId);
        var good = rig.Store.LoadJournal(rig.ProjectId, run.RunId)!.Actions.Single();
        var executor = new ProjectActionExecutor(rig.Authorizer, new TestFilesFactory());
        var before = rig.Project.Fingerprint();

        foreach (var target in new[] { "src/Program.cs", ".claude/settings.json", ".mariliz/decisions/../../x.md", "mariliz.yaml", "C:/Windows/x.md" })
        {
            var bad = Clone(good);
            bad.Target = target;
            Assert.Equal(JournalAction.Failed, executor.Execute(rig.ProjectId, bad).Status);
        }

        var swapped = Clone(good);
        swapped.Content = "# outro conteúdo";
        Assert.Equal(JournalAction.Failed, executor.Execute(rig.ProjectId, swapped).Status);

        var command = Clone(good);
        command.Kind = "command.run";
        Assert.Equal(JournalAction.Failed, executor.Execute(rig.ProjectId, command).Status);
        Assert.Equal(before, rig.Project.Fingerprint());
    }

    private static JournalAction Clone(JournalAction a) => new()
    {
        Id = a.Id, Kind = a.Kind, Summary = a.Summary, Target = a.Target, Evidence = [.. a.Evidence], Content = a.Content, ContentSha256 = a.ContentSha256, Status = JournalAction.Approved
    };

    [Fact]
    public async Task No_protocolo_o_identificador_e_as_acoes_tem_formato_e_sem_actionIds_nada_e_aprovado()
    {
        using var rig = new Rig();
        var runtime = new ProposingRuntime(ex => [Proposal(ex)]);
        var (service, tracker) = rig.NewEngine(runtime);
        var session = new EngineSession("test", workspace: rig.Authorizer, workflows: service, tracker: tracker);
        var (run, approvalId, executionId) = await PauseAsync(service, rig.ProjectId);

        JsonObject Resume(JsonObject extra)
        {
            var p = new JsonObject { ["projectId"] = rig.ProjectId, ["runId"] = run.RunId };
            foreach (var (k, v) in extra) p[k] = v?.DeepClone();
            return session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "r1", ["method"] = "workflow.resume", ["params"] = p }.ToJsonString()).Response;
        }

        Assert.Equal("INVALID_PARAMS", Resume(new JsonObject { ["approvalId"] = "qualquer" })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", Resume(new JsonObject { ["approvalId"] = approvalId, ["actionIds"] = new JsonArray("rm -rf /") })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", Resume(new JsonObject { ["actionIds"] = "act-decisao-impacto" })["error"]!["code"]!.GetValue<string>());
        Assert.Equal("APPROVAL_MISMATCH", Resume(new JsonObject { ["approvalId"] = "apr-ffffffffffff", ["actionIds"] = new JsonArray("act-decisao-impacto") })["error"]!["code"]!.GetValue<string>());
        Assert.False(File.Exists(DecisionPath(rig.Project, executionId)));

        var ok = Resume(new JsonObject { ["approvalId"] = approvalId, ["actionIds"] = new JsonArray("act-decisao-impacto") });
        Assert.Equal(run.RunId, ok["result"]!["runId"]!.GetValue<string>());
        await run.Completion!;
        Assert.True(File.Exists(DecisionPath(rig.Project, executionId)));
    }
}
