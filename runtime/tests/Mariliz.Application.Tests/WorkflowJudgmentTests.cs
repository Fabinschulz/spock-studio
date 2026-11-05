using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// A opinião do juiz por modelo (Fase 7) é do runtime de IA, mas só vale para o motor se o proxy viu a chamada. Aqui o runtime
/// é um dublê que afirma coisas, para provar o que o motor aceita, o que descarta e o que registra.
/// </summary>
public class WorkflowJudgmentTests
{
    private const string Chunk = "docs/nota.md#1";
    private static readonly ModelCallRecord JudgeCall = new("judge", "judge", "gpt-4o-mini", "gpt-4o-mini-2024-07-18", new string('a', 64), 700, 90);

    private static JsonObject Judgment(int plan = 4, bool withImpact = false, JsonArray? evidence = null, string rationale = "O plano é concreto.")
    {
        var scores = new JsonObject { ["plan_quality"] = plan, ["context_fit"] = 4, ["request_fit"] = 5 };
        if (withImpact) scores["architecture_coherence"] = 4;
        return new JsonObject
        {
            ["schema"] = "engineering.judgment.v1",
            ["criteriaVersion"] = "1",
            ["scores"] = scores,
            ["rationale"] = rationale,
            ["evidence"] = evidence ?? new JsonArray(Chunk),
            ["model"] = "o-que-o-runtime-diz",
            ["modelVersion"] = "qualquer"
        };
    }

    private static WorkflowAck Ack(string judge, JsonObject? judgment, bool withImpact = false)
    {
        var evaluation = new JsonObject { ["schema"] = "engineering.evaluation.v1", ["verdict"] = "deterministic", ["judge"] = judge, ["tokens"] = 0 };
        if (judgment is not null) evaluation["judgment"] = judgment;
        var output = new JsonObject
        {
            ["summary"] = "",
            ["stacks"] = new JsonArray(),
            ["evidence"] = new JsonArray(new JsonObject { ["chunkId"] = Chunk, ["filePath"] = "docs/nota.md", ["hash"] = new string('b', 64), ["score"] = 1.0, ["excerpt"] = "x" }),
            ["evaluation"] = evaluation
        };
        if (withImpact) output["impact"] = new JsonObject { ["schema"] = "engineering.impact.v1", ["areas"] = new JsonArray("servico"), ["risks"] = new JsonArray(), ["evidence"] = new JsonArray(Chunk) };
        return new WorkflowAck(WorkflowService.WorkflowId, "run-aaaaaaaaaaaa", "p-aaaaaaaaaaaa", "ex-aaaaaaaaaaaa", "completed", output, [], [], null);
    }

    [Fact]
    public void Opiniao_conferida_usa_o_modelo_a_versao_o_hash_e_os_tokens_do_proxy_e_nao_os_do_runtime()
    {
        var verified = JudgmentVerifier.Verify(Ack("passed", Judgment()), [JudgeCall]);

        Assert.NotNull(verified);
        Assert.Equal("passed", verified.Outcome);
        Assert.Equal(("gpt-4o-mini", "gpt-4o-mini-2024-07-18"), (verified.Model, verified.ModelVersion));
        Assert.Equal(new string('a', 64), verified.PromptHash);
        Assert.Equal((700, 90), (verified.InputTokens, verified.OutputTokens));
        Assert.Equal([Chunk], verified.Evidence);
    }

    [Fact]
    public void Sem_chamada_de_juiz_vista_pelo_proxy_a_opiniao_nao_existe()
    {
        Assert.Null(JudgmentVerifier.Verify(Ack("passed", Judgment()), []));
        // Chamadas de outros agentes não valem como a do juiz.
        var planning = JudgeCall with { AgentId = "planning", Purpose = "plan" };
        Assert.Null(JudgmentVerifier.Verify(Ack("passed", Judgment()), [planning]));
        Assert.Null(JudgmentVerifier.Verify(Ack("passed", Judgment()), [JudgeCall with { Purpose = "plan" }]));
    }

    [Fact]
    public void O_que_o_runtime_afirma_fora_do_contrato_e_descartado()
    {
        var cases = new List<WorkflowAck>
        {
            Ack("passed", Judgment(plan: 2)), // a nota não bate com o resultado declarado
            Ack("failed", Judgment(plan: 4)), // e o contrário
            Ack("passed", Judgment(plan: 9)),
            Ack("passed", Judgment(plan: 0)),
            Ack("passed", Judgment(withImpact: true)), // critério de impacto sem impacto no resultado
            Ack("passed", Judgment(rationale: "")),
            Ack("passed", Judgment(rationale: new string('x', 301))),
            Ack("passed", Judgment(evidence: new JsonArray("docs/inventado.md#9"))), // citação fora da recuperação
            Ack("passed", Judgment(evidence: new JsonArray())), // havia trechos: precisa citar
            Ack("passed", null),
            Ack("not_run", Judgment())
        };
        cases.Add(Ack("passed", Judgment(), withImpact: true)); // impacto exige o critério de arquitetura

        foreach (var ack in cases)
            Assert.Null(JudgmentVerifier.Verify(ack, [JudgeCall]));
    }

    [Fact]
    public void Segredo_na_justificativa_sai_redigido()
    {
        var verified = JudgmentVerifier.Verify(Ack("passed", Judgment(rationale: "Usa a chave AKIAABCDEFGHIJKLMNOP no exemplo.")), [JudgeCall]);

        Assert.NotNull(verified);
        Assert.DoesNotContain("AKIAABCDEFGHIJKLMNOP", verified.Rationale);
    }

    [Fact]
    public void O_registro_em_disco_e_o_bloco_do_resultado_seguem_os_contratos_e_o_veredito_continua_deterministico()
    {
        var verified = JudgmentVerifier.Verify(Ack("failed", Judgment(plan: 2)), [JudgeCall])!;

        var record = JudgmentRecord.ForDisk(verified, "run-aaaaaaaaaaaa", "p-aaaaaaaaaaaa", "ex-aaaaaaaaaaaa", DateTimeOffset.UtcNow);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "engineering.eval.record.v1.schema.json"), record));
        Assert.Null(record["verdict"]);

        var evaluation = JudgmentRecord.Evaluation(verified);
        Assert.Equal("deterministic", evaluation["verdict"]!.GetValue<string>());
        Assert.Equal("failed", evaluation["judge"]!.GetValue<string>());
        Assert.Equal(790, evaluation["tokens"]!.GetValue<int>());

        var none = JudgmentRecord.Evaluation(null);
        Assert.Equal("not_run", none["judge"]!.GetValue<string>());
        Assert.Null(none["judgment"]);
    }

    private sealed class FakeAccess(IReadOnlyList<ModelCallRecord> calls) : IModelAccess
    {
        public bool Configured => true;
        public ModelGrant Open(string executionId, int tokenBudget) => new("http://127.0.0.1:49152/v1/model", new string('t', 43), tokenBudget);
        public ModelUsage Close(string executionId) => new(calls.Count, calls.Sum(c => c.InputTokens), calls.Sum(c => c.OutputTokens), 0);
        public IReadOnlyList<ModelCallRecord> Calls(string executionId) => calls;
    }

    private sealed class SpyRecords : IEvalRecords
    {
        public List<(string ProjectId, string ExecutionId, JsonObject Record)> Saved { get; } = [];
        public void Save(string projectId, string executionId, JsonObject record) => Saved.Add((projectId, executionId, record));
    }

    /// <summary>Runtime que anuncia a avaliação por modelo e conclui com o ack que o teste mandou.</summary>
    private sealed class JudgingRuntime(string outcome, Func<string, JsonObject> ack) : IAiRuntime
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
            onEvent(Event(executionId, 0, "eval.started", new JsonObject { ["evalId"] = "EV-JUDGE-001", ["kind"] = "model" }));
            onEvent(Event(executionId, 1, "eval.completed", new JsonObject { ["evalId"] = "EV-JUDGE-001", ["kind"] = "model", ["outcome"] = outcome, ["criteriaVersion"] = "1" }));
            var document = ack(executionId);
            var ackOutput = (JsonObject)document["output"]!.DeepClone();
            return Task.FromResult(new WorkflowAck(WorkflowService.WorkflowId, request["runId"]!.GetValue<string>(), request["projectId"]!.GetValue<string>(),
                executionId, "completed", ackOutput, [], [], null));
        }

        private static JsonObject Event(string executionId, int seq, string type, JsonObject data) => new()
        {
            ["schema"] = "ai.event.v1",
            ["eventId"] = $"{executionId}:{seq}",
            ["executionId"] = executionId,
            ["localSeq"] = seq,
            ["type"] = type,
            ["at"] = "2026-11-05T19:00:00.000+00:00",
            ["data"] = data
        };
    }

    private static async Task<(RunHandle Run, SpyRecords Records, RunJournal Journal)> RunWith(string outcome, JsonObject output, IReadOnlyList<ModelCallRecord> calls)
    {
        using var project = TempProject.Dotnet();
        var authorizer = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = authorizer.Authorize(project.Root).Project!.ProjectId;
        var store = new FileRunStore(project.DataDir);
        var records = new SpyRecords();
        var service = new WorkflowService(authorizer, new JudgingRuntime(outcome, _ => new JsonObject { ["output"] = output }), new RunTracker(), new SystemClock(), store,
            WorkflowContextBuilder.WithoutProject, new FakeAccess(calls), records);

        var run = await service.StartAsync(projectId, new JsonObject { ["goal"] = "descrever", ["useModel"] = true }, CancellationToken.None);
        await run.Completion!;
        return (run, records, store.LoadJournal(projectId, run.RunId)!);
    }

    private static List<JsonObject> EvalEvents(RunHandle run) =>
        run.Snapshot()["events"]!.AsArray().Cast<JsonObject>().Where(e => e["type"]!.GetValue<string>().StartsWith("eval.", StringComparison.Ordinal)).ToList();

    [Fact]
    public async Task Com_a_chamada_do_juiz_vista_o_motor_registra_publica_e_poe_no_resultado()
    {
        var (run, records, journal) = await RunWith("passed", Ack("passed", Judgment()).Output, [JudgeCall]);

        var saved = Assert.Single(records.Saved);
        Assert.Equal("gpt-4o-mini-2024-07-18", saved.Record["modelVersion"]!.GetValue<string>());
        Assert.Equal(new string('a', 64), saved.Record["promptHash"]!.GetValue<string>());
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "engineering.eval.record.v1.schema.json"), saved.Record));

        var evaluation = run.Result!["evaluation"]!;
        Assert.Equal("deterministic", evaluation["verdict"]!.GetValue<string>());
        Assert.Equal("passed", evaluation["judge"]!.GetValue<string>());
        Assert.Equal("gpt-4o-mini", evaluation["judgment"]!["model"]!.GetValue<string>());

        Assert.Equal("passed", journal.Judgment!.Outcome);
        Assert.Contains(journal.Evals, e => e.Id == "EV-JUDGE-001" && e.Status == "passed" && !e.Required);

        var events = EvalEvents(run);
        Assert.Equal(["eval.started", "eval.completed"], events.Select(e => e["type"]!.GetValue<string>()));
        foreach (var evt in events)
            Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "eval.data.schema.json"), evt["data"]), evt.ToJsonString());
        var completed = events[1]["data"]!;
        Assert.Equal("passed", completed["outcome"]!.GetValue<string>());
        Assert.Equal("gpt-4o-mini-2024-07-18", completed["modelVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task O_runtime_que_afirma_uma_opiniao_sem_chamada_no_proxy_nao_a_ve_registrada_nem_publicada()
    {
        var (run, records, journal) = await RunWith("passed", Ack("passed", Judgment()).Output, []);

        Assert.Empty(records.Saved);
        Assert.Null(journal.Judgment);
        Assert.Equal("not_run", run.Result!["evaluation"]!["judge"]!.GetValue<string>());
        Assert.Null(run.Result["evaluation"]!["judgment"]);
        Assert.DoesNotContain(journal.Evals, e => e.Id == "EV-JUDGE-001");
        var completed = EvalEvents(run)[1]["data"]!;
        Assert.Equal("not_run", completed["outcome"]!.GetValue<string>());
        Assert.Null(completed["model"]);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("ai", "eval.data.schema.json"), completed));
    }

    [Fact]
    public async Task Opiniao_fora_do_contrato_nao_e_registrada_mesmo_com_a_chamada_vista()
    {
        var (run, records, journal) = await RunWith("passed", Ack("passed", Judgment(plan: 9)).Output, [JudgeCall]);

        Assert.Empty(records.Saved);
        Assert.Null(journal.Judgment);
        Assert.Equal("not_run", run.Result!["evaluation"]!["judge"]!.GetValue<string>());
    }

    [Fact]
    public async Task Avaliacao_por_modelo_nunca_muda_o_estado_da_execucao()
    {
        var (passed, _, _) = await RunWith("passed", Ack("passed", Judgment()).Output, [JudgeCall]);
        var (failed, _, _) = await RunWith("failed", Ack("failed", Judgment(plan: 1)).Output, [JudgeCall]);
        var (none, _, _) = await RunWith("not_run", Ack("not_run", null).Output, []);

        Assert.Equal(RunStates.CompletedWithLimitations, passed.State);
        Assert.Equal(passed.State, failed.State);
        Assert.Equal(passed.State, none.State);
    }

    [Fact]
    public void O_registro_vai_para_a_pasta_de_avaliacoes_do_projeto_fora_do_repositorio()
    {
        using var project = TempProject.Dotnet();
        var verified = JudgmentVerifier.Verify(Ack("passed", Judgment()), [JudgeCall])!;
        var record = JudgmentRecord.ForDisk(verified, "run-aaaaaaaaaaaa", "p-aaaaaaaaaaaa", "ex-aaaaaaaaaaaa", DateTimeOffset.UtcNow);

        new FileEvalRecords(project.DataDir).Save("p-aaaaaaaaaaaa", "ex-aaaaaaaaaaaa", record);

        var path = Path.Combine(project.DataDir, "projects", "p-aaaaaaaaaaaa", "evals", "ex-aaaaaaaaaaaa.json");
        Assert.True(File.Exists(path));
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("intelligence", "engineering.eval.record.v1.schema.json"), JsonNode.Parse(File.ReadAllText(path))));
        Assert.Throws<ArgumentException>(() => new FileEvalRecords(project.DataDir).Save("../fora", "ex-aaaaaaaaaaaa", record));
        Assert.Throws<ArgumentException>(() => new FileEvalRecords(project.DataDir).Save("p-aaaaaaaaaaaa", "..\\x", record));
    }
}
