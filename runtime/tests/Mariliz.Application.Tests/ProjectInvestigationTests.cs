using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Timeline;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// A investigação de gestão no motor: os fatos que vão ao runtime, o que volta e como é conferido, e o diário. O runtime é um
/// dublê que devolve o resultado escrito no teste, com afirmações boas e ruins de propósito.
/// </summary>
public class ProjectInvestigationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Question = "Por que a vazão caiu? SEGREDO-DA-PERGUNTA";

    private sealed class FakeRuntime : IInvestigationRuntime
    {
        public Func<JsonObject, JsonObject>? Reply { get; set; }
        public TimeSpan Delay { get; set; }
        public JsonObject? Seen { get; private set; }
        public int Calls { get; private set; }

        public async Task<JsonObject> InvestigateAsync(JsonObject request, Action<JsonObject> onEvent, CancellationToken ct)
        {
            Calls++;
            Seen = request.DeepClone().AsObject();
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            return Reply!(request);
        }
    }

    private sealed class FakeModelAccess(bool configured = true) : IModelAccess
    {
        public bool Configured { get; } = configured;
        public List<string> Opened { get; } = [];
        public List<string> Closed { get; } = [];

        public ModelGrant Open(string executionId, int tokenBudget)
        {
            Opened.Add(executionId);
            return new ModelGrant("http://127.0.0.1:49152/v1/model", new string('t', 43), tokenBudget);
        }

        public ModelUsage Close(string executionId)
        {
            Closed.Add(executionId);
            return new ModelUsage(3, 2400, 600, 0);
        }

        public IReadOnlyList<ModelCallRecord> Calls(string executionId) =>
            [new ModelCallRecord("investigator", "investigate", "gpt-4o-mini", "gpt-4o-mini-2024-07-18", "hash", 800, 200)];
    }

    private sealed class Setup : IDisposable
    {
        public IntegrationHarness Harness { get; } = new();
        public FakeRuntime Runtime { get; } = new();
        public FakeModelAccess Models { get; }
        public FileRunStore Store { get; }
        public InvestigationService Service { get; }
        public DecisionService Decisions { get; }
        public EngineSession Session { get; }
        public ProjectIntegration Integration { get; private set; } = null!;

        public Setup(bool modelConfigured = true, bool withRuntime = true, IInvestigationRuntime? runtime = null, string? indexRoot = null, IModelAccess? modelAccess = null)
        {
            Harness.Clock.UtcNow = Now;
            Models = new FakeModelAccess(modelConfigured);
            Store = new FileRunStore(Harness.DataDir);
            var management = new ManagementService(Harness.Workspace, Harness.Hub, Harness.Clock);
            var assistant = new ManagementAssistant(management, Harness.Hub, Harness.Workspace, new ModelGateway(new NoSettings(), new NoTransport()), Harness.Clock, Store);
            var timeline = new ProjectTimelineService(Harness.Workspace, Harness.Clock, Store, management: management, integrations: Harness.Hub);
            var decisionStore = new FileDecisionStore(Harness.DataDir);
            Decisions = new DecisionService(Harness.Workspace, decisionStore, Store,
                id => new EvidencePack(id, "loja", Harness.Clock.UtcNow, [new EvidenceFact("today", "contexto", "Hoje é 2026-11-10.")], []), Harness.Audit, Harness.Clock);
            Service = new InvestigationService(assistant, Harness.Hub, Harness.Workspace, runtime ?? Runtime, Harness.Clock, Store, modelAccess ?? Models, timeline,
                new DecisionMemory(Harness.Workspace, decisionStore), indexRoot);
            Session = new EngineSession("test", integrations: Harness.Hub, management: management, assistant: assistant,
                investigation: withRuntime ? Service : null);
        }

        public async Task SeedAsync()
        {
            Integration = await Harness.LinkGitHubAsync();
            var data = new SyncedData();
            for (var week = 0; week < 8; week++)
                for (var n = 0; n < 2; n++)
                {
                    var closed = Now.AddDays(-7 * (8 - week) + 3 + n * 0.1);
                    data.Upsert(Item($"d{week}{n}", StatusCategories.Done, closed.AddDays(-2), closed), Now);
                }
            data.Upsert(Item("41", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1)), Now);
            data.Upsert(Item("42", StatusCategories.InProgress, Now.AddDays(-5)), Now);
            data.Record(new SyncExecution("sync-0000000000aa", Now.AddDays(-1), Now.AddDays(-1), SyncStatus.Succeeded, false, 1, data.Items.Count, data.Items.Count, 0, 0, null));
            new FileSyncedDataStore(Harness.DataDir).Write(Harness.ProjectId, Integration.Id, data);
        }

        public ManagementDecision RecordDecision(string title, string decision, string? rationale = null)
        {
            Harness.Clock.UtcNow = Harness.Clock.UtcNow.AddMinutes(1);
            return Decisions.Record(Harness.ProjectId, title, decision, rationale, []).Decision;
        }

        public void Journal(string runId, string kind, string state, double daysAgo) =>
            Store.SaveJournal(new RunJournal
            {
                RunId = runId, ProjectId = Harness.ProjectId, Kind = kind, State = state,
                StartedAt = Now.AddDays(-daysAgo), FinishedAt = Now.AddDays(-daysAgo).AddMinutes(1)
            });

        public JsonObject Request(string method, JsonObject parameters) =>
            Session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

        public void Dispose() => Harness.Dispose();
    }

    private sealed class NoSettings : IModelSettings
    {
        public ModelStatus Status() => new(false, "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = "";
            return false;
        }
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException();
    }

    private static WorkItem Item(string id, string category, DateTimeOffset created, DateTimeOffset? closed = null, DateOnly? due = null) =>
        new(DataKinds.Issues, id, "Titulo secreto " + id, "Issue", category == StatusCategories.Done ? "completed" : "open", category, [], [],
            created, closed ?? created, closed, due, null, "https://example.test/" + id, []);

    private static JsonObject Claim(string text, params string[] evidence) =>
        new() { ["text"] = text, ["evidence"] = new JsonArray(evidence.Select(id => (JsonNode)id).ToArray()) };

    private static JsonArray Claims(params JsonObject[] items) => new(items.Select(item => (JsonNode)item).ToArray());

    private static JsonObject GoodAnswer() => new()
    {
        ["summary"] = Claim("Há 2 itens abertos.", "flow.issues.items"),
        ["observed"] = Claims(Claim("Issues: 18 itens sincronizados, 2 abertos.", "flow.issues.items")),
        ["calculations"] = new JsonArray(),
        ["inferences"] = Claims(Claim("O prazo vencido pode estar atrasando a vazão.", "risk.overdue")),
        ["recommendations"] = new JsonArray(),
        ["unavailable"] = new JsonArray(new JsonObject { ["text"] = "Os fatos não dizem o motivo." })
    };

    private static JsonArray GoodPath(params string[] facts) => new()
    {
        new JsonObject { ["id"] = "frame", ["title"] = "Pergunta enquadrada", ["detail"] = "Começa por: contexto, indicadores.", ["round"] = 0, ["groups"] = new JsonArray("contexto", "indicadores"), ["facts"] = new JsonArray() },
        new JsonObject
        {
            ["id"] = "gather", ["title"] = "Fatos reunidos (rodada 1)", ["detail"] = "Grupos: contexto, indicadores.", ["round"] = 1,
            ["groups"] = new JsonArray("contexto", "indicadores"), ["facts"] = new JsonArray(facts.Select(id => (JsonNode)id).ToArray())
        },
        new JsonObject { ["id"] = "conclude", ["title"] = "Conclusão", ["detail"] = "Escrita pelo modelo, a conferir pelo motor.", ["round"] = 0, ["groups"] = new JsonArray(), ["facts"] = new JsonArray() }
    };

    private static JsonObject Ack(JsonObject request, JsonObject? answer = null, string status = "completed", JsonArray? consulted = null, JsonArray? path = null,
        JsonArray? limitations = null, JsonArray? rejected = null, JsonArray? retrieved = null) => new()
    {
        ["schema"] = "ai.investigation.ack.v1",
        ["investigationId"] = request["investigationId"]!.GetValue<string>(),
        ["executionId"] = request["executionId"]!.GetValue<string>(),
        ["projectId"] = request["projectId"]!.GetValue<string>(),
        ["status"] = status,
        ["answer"] = answer ?? GoodAnswer(),
        ["rejected"] = rejected ?? new JsonArray(),
        ["path"] = path ?? GoodPath("today", "flow.issues.items", "risk.overdue"),
        ["consulted"] = consulted ?? new JsonArray("today", "flow.issues.items", "risk.overdue"),
        ["retrieved"] = retrieved ?? new JsonArray(),
        ["limitations"] = limitations ?? new JsonArray(),
        ["rounds"] = 1,
        ["usage"] = new JsonObject { ["modelCalls"] = 2, ["inputTokens"] = 100, ["outputTokens"] = 50, ["tokens"] = 150 }
    };

    // ------------------------------------------------------------------ Fatos

    [Fact]
    public async Task O_pacote_traz_os_fatos_do_assistente_e_os_de_execucoes_e_de_mudancas()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Journal("run-aaaaaaaaaaaa", RunKinds.Workflow, RunStates.Failed, 3);
        s.Journal("run-bbbbbbbbbbbb", RunKinds.Validate, RunStates.Completed, 2);
        s.Journal("ask-cccccccccccc", RunKinds.Assistant, RunStates.Failed, 1);
        s.Journal("inv-dddddddddddd", RunKinds.Investigation, RunStates.Failed, 1);
        s.Journal("run-eeeeeeeeeeee", RunKinds.Apply, RunStates.Failed, 60);

        var pack = s.Service.Pack(s.Harness.ProjectId);
        var facts = pack.Facts.ToDictionary(fact => fact.Id);

        Assert.Contains("flow.issues.items", facts.Keys);
        Assert.Equal("execuções", facts["runs.summary"].Group);
        Assert.Equal(
            "Execuções do projeto nos últimos 28 dias: 2 (1 validação, 1 pedido de engenharia); 1 falhou, 0 terminaram com limitações, 0 foram revertidas ou desfeitas.",
            facts["runs.summary"].Text);
        Assert.Equal("Execuções que falharam nos últimos 28 dias, por tipo: 1 pedido de engenharia.", facts["runs.failed"].Text);
        Assert.Equal("mudanças", facts["changes.summary"].Group);
        Assert.StartsWith("Mudanças no projeto nos últimos 28 dias", facts["changes.summary"].Text);
        Assert.Contains("1 sincronização", facts["changes.summary"].Text);
    }

    [Fact]
    public async Task Sem_execucao_nem_mudanca_o_pacote_diz_isso_e_titulo_de_item_nao_vai()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var pack = s.Service.Pack(s.Harness.ProjectId);

        Assert.Equal("Nenhuma execução do projeto nos últimos 28 dias.", pack.Facts.Single(fact => fact.Id == "runs.summary").Text);
        Assert.DoesNotContain(pack.Facts, fact => fact.Text.Contains("Titulo secreto", StringComparison.Ordinal));
    }

    [Fact]
    public async Task So_vao_ao_runtime_fatos_que_o_contrato_aceita()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var facts = s.Runtime.Seen!["facts"]!.AsArray();
        Assert.NotEmpty(facts);
        Assert.All(facts, node =>
        {
            Assert.Matches(new Regex(@"^[A-Za-z0-9][A-Za-z0-9._:-]{0,63}$"), node!["id"]!.GetValue<string>());
            Assert.InRange(node["text"]!.GetValue<string>().Length, 1, 1200);
            Assert.InRange(node["group"]!.GetValue<string>().Length, 1, 40);
        });
        Assert.Equal(facts.Count, facts.Select(node => node!["id"]!.GetValue<string>()).Distinct().Count());
    }

    // ------------------------------------------------------------------ O pedido

    [Fact]
    public async Task Com_modelo_configurado_o_pedido_leva_o_proxy_e_o_teto_de_tokens_e_o_acesso_fecha()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        await s.Service.InvestigateAsync(s.Harness.ProjectId, "  " + Question + "  ", CancellationToken.None);

        var seen = s.Runtime.Seen!;
        Assert.Equal("ai.investigation.start.v1", seen["schema"]!.GetValue<string>());
        Assert.Matches(@"^inv-[0-9a-f]{12}$", seen["investigationId"]!.GetValue<string>());
        Assert.Matches(@"^ex-[0-9a-f]{12}$", seen["executionId"]!.GetValue<string>());
        Assert.Equal(Question, seen["question"]!.GetValue<string>());
        Assert.True(seen["policy"]!["modelAllowed"]!.GetValue<bool>());
        Assert.Equal(InvestigationService.TokenBudget, seen["limits"]!["maxTokens"]!.GetValue<int>());
        Assert.Equal("http://127.0.0.1:49152/v1/model", seen["broker"]!["url"]!.GetValue<string>());
        Assert.Equal(s.Models.Opened, s.Models.Closed);
        Assert.Equal(seen["executionId"]!.GetValue<string>(), Assert.Single(s.Models.Closed));
    }

    [Fact]
    public async Task Sem_modelo_configurado_o_pedido_nao_leva_proxy_nem_tokens()
    {
        using var s = new Setup(modelConfigured: false);
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request, status: "completed_with_limitations");

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var seen = s.Runtime.Seen!;
        Assert.False(seen["policy"]!["modelAllowed"]!.GetValue<bool>());
        Assert.Equal(0, seen["limits"]!["maxTokens"]!.GetValue<int>());
        Assert.Null(seen["broker"]);
        Assert.Empty(s.Models.Opened);
        Assert.False(result.UsedModel);
        Assert.Equal(0, result.Answer.InputTokens);
    }

    [Fact]
    public async Task Pergunta_curta_ou_longa_demais_e_recusada_antes_de_chamar_o_runtime_e_nao_vira_execucao()
    {
        using var s = new Setup();
        await s.SeedAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => s.Service.InvestigateAsync(s.Harness.ProjectId, "oi", CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => s.Service.InvestigateAsync(s.Harness.ProjectId, new string('x', 501), CancellationToken.None));

        Assert.Equal(0, s.Runtime.Calls);
        Assert.Empty(s.Store.ListJournals(s.Harness.ProjectId));
    }

    [Fact]
    public async Task Projeto_nao_autorizado_e_recusado()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var ex = await Assert.ThrowsAsync<EngineException>(() => s.Service.InvestigateAsync("p-ffffffffffff", Question, CancellationToken.None));

        Assert.Equal("PROJECT_NOT_AUTHORIZED", ex.Code);
        Assert.Equal(0, s.Runtime.Calls);
    }

    // ------------------------------------------------------------------ A conferência

    [Fact]
    public async Task A_resposta_boa_passa_com_as_citacoes_e_o_uso_vem_do_proxy_do_motor()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        Assert.Equal("Há 2 itens abertos.", result.Answer.Summary);
        Assert.Equal(["flow.issues.items"], Assert.Single(result.Answer.Observed).Evidence);
        Assert.Equal(["Os fatos não dizem o motivo."], result.Answer.Unavailable);
        Assert.Empty(result.Answer.Rejected);
        Assert.Equal(["flow.issues.items", "risk.overdue"], result.Answer.Evidence.Select(fact => fact.Id).Order());
        Assert.Equal(2400, result.Answer.InputTokens);
        Assert.Equal(600, result.Answer.OutputTokens);
        Assert.Equal("gpt-4o-mini-2024-07-18", result.Answer.Model);
        Assert.True(result.UsedModel);
        Assert.Equal(1, result.Rounds);
    }

    [Fact]
    public async Task Citar_um_fato_que_existe_mas_nao_foi_consultado_nao_passa()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var answer = GoodAnswer();
        // flow.issues.wip existe no pacote, mas a investigação só consultou três fatos.
        answer["observed"] = Claims(Claim("Issues em andamento agora: 1.", "flow.issues.wip"), Claim("Issues: 18 itens sincronizados, 2 abertos.", "flow.issues.items"));
        s.Runtime.Reply = request => Ack(request, answer);

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        Assert.Equal(["Issues: 18 itens sincronizados, 2 abertos."], result.Answer.Observed.Select(item => item.Text));
        Assert.Contains(result.Answer.Rejected, item => item.Text.StartsWith("Issues em andamento", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Numero_que_os_fatos_citados_nao_tem_e_descartado_pelo_motor()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var answer = GoodAnswer();
        answer["observed"] = Claims(Claim("Há 99 itens abertos.", "flow.issues.items"));
        s.Runtime.Reply = request => Ack(request, answer);

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        Assert.Empty(result.Answer.Observed);
        var rejected = Assert.Single(result.Answer.Rejected);
        Assert.Contains("99", rejected.Reason);
    }

    [Fact]
    public async Task O_que_o_runtime_descartou_aparece_com_o_motivo_em_portugues()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var rejected = new JsonArray(new JsonObject { ["text"] = "Sem citação.", ["reason"] = "no_valid_evidence" });
        s.Runtime.Reply = request => Ack(request, rejected: rejected);

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var item = Assert.Single(result.Answer.Rejected);
        Assert.Equal("Não cita nenhum fato consultado na investigação.", item.Reason);
    }

    [Fact]
    public async Task O_caminho_so_fala_de_etapas_e_fatos_que_existem()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var path = GoodPath("today", "fato-inventado", "flow.issues.items");
        path.Add(new JsonObject { ["id"] = "improvisar", ["title"] = "Etapa inventada", ["detail"] = "x", ["round"] = 9, ["groups"] = new JsonArray(), ["facts"] = new JsonArray() });
        s.Runtime.Reply = request => Ack(request, path: path, consulted: new JsonArray("today", "fato-inventado", "flow.issues.items", "risk.overdue"));

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        Assert.Equal(["frame", "gather", "conclude"], result.Path.Select(step => step.Id));
        Assert.Equal(["today", "flow.issues.items"], result.Path[1].Facts);
        Assert.Equal(1, result.Path[1].Round);
    }

    [Theory]
    [InlineData("failed", "INVESTIGATION_FAILED")]
    [InlineData("rejected", "INVESTIGATION_REJECTED")]
    [InlineData("outra-coisa", "AI_RUNTIME_INVALID_RESPONSE")]
    public async Task Resultado_que_nao_e_de_sucesso_vira_erro_com_codigo_e_deixa_diario_de_falha(string status, string code)
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request =>
        {
            var ack = Ack(request, status: status);
            ack["failure"] = "LIMIT_ITERATIONS";
            return ack;
        };

        var ex = await Assert.ThrowsAsync<EngineException>(() => s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None));

        Assert.Equal(code, ex.Code);
        var journal = Assert.Single(s.Store.ListJournals(s.Harness.ProjectId));
        Assert.Equal(RunKinds.Investigation, journal.Kind);
        Assert.Equal(RunStates.Failed, journal.State);
        Assert.Equal(code, journal.Error);
        Assert.Single(s.Models.Closed);
    }

    [Fact]
    public async Task Ack_de_outra_investigacao_ou_de_outro_schema_esta_fora_do_contrato()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request =>
        {
            var ack = Ack(request);
            ack["investigationId"] = "inv-000000000000";
            return ack;
        };
        var other = await Assert.ThrowsAsync<EngineException>(() => s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None));
        s.Runtime.Reply = request =>
        {
            var ack = Ack(request);
            ack["schema"] = "ai.ack.v3";
            return ack;
        };
        var schema = await Assert.ThrowsAsync<EngineException>(() => s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None));

        Assert.Equal("AI_RUNTIME_INVALID_RESPONSE", other.Code);
        Assert.Equal("AI_RUNTIME_INVALID_RESPONSE", schema.Code);
    }

    [Fact]
    public async Task Passar_do_prazo_vira_erro_de_tempo_e_o_acesso_ao_modelo_fecha()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Delay = TimeSpan.FromSeconds(30);
        s.Runtime.Reply = request => Ack(request);
        var service = new InvestigationService(
            new ManagementAssistant(new ManagementService(s.Harness.Workspace, s.Harness.Hub, s.Harness.Clock), s.Harness.Hub, s.Harness.Workspace,
                new ModelGateway(new NoSettings(), new NoTransport()), s.Harness.Clock, s.Store),
            s.Harness.Hub, s.Harness.Workspace, s.Runtime, s.Harness.Clock, s.Store, s.Models) { Deadline = TimeSpan.FromMilliseconds(100) };

        var ex = await Assert.ThrowsAsync<EngineException>(() => service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None));

        Assert.Equal("INVESTIGATION_TIMEOUT", ex.Code);
        Assert.Single(s.Models.Closed);
    }

    // ------------------------------------------------------------------ O diário

    [Fact]
    public async Task O_diario_guarda_o_caminho_com_os_ids_dos_fatos_e_o_uso_e_nunca_o_texto_da_pergunta()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request, limitations: new JsonArray(new JsonObject { ["code"] = "INVESTIGATION_STOPPED", ["message"] = "A investigação parou antes de olhar: riscos." }));

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var journal = s.Store.LoadJournal(s.Harness.ProjectId, result.RunId)!;
        Assert.Equal(RunKinds.Investigation, journal.Kind);
        Assert.Equal(RunStates.CompletedWithLimitations, journal.State);
        Assert.Equal(["frame-0", "gather1-1", "conclude-2", "verify"], journal.Steps.Select(step => step.Id));
        Assert.Contains("Fatos: today, flow.issues.items, risk.overdue.", journal.Steps[1].Detail);
        Assert.Contains("A investigação parou antes de olhar: riscos.", journal.Pending);
        Assert.Equal(3, journal.ModelUsage!.Calls);
        Assert.Equal(2400, journal.ModelUsage.InputTokens);
        var written = System.Text.Json.JsonSerializer.Serialize(journal);
        Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", written, StringComparison.Ordinal);
        Assert.DoesNotContain("Há 2 itens abertos", written, StringComparison.Ordinal);
        Assert.DoesNotContain("Titulo secreto", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_auditoria_registra_contagens_e_nao_o_texto_da_pergunta()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var entry = Assert.Single(s.Harness.Audit.Entries, item => item.Action == "management.investigation_answered");
        Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", entry.Detail, StringComparison.Ordinal);
        Assert.Contains("\"modelCalls\":3", entry.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uma_investigacao_aparece_na_central_de_execucoes_como_investigacao()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        var catalog = new RunCatalog(s.Store, s.Harness.Workspace, new RunTracker());
        var listed = catalog.List(s.Harness.ProjectId, [RunKinds.Investigation], [], null, null);
        var run = Assert.Single(listed["runs"]!.AsArray())!;
        Assert.Contains(RunKinds.Investigation, RunCatalog.Kinds);
        Assert.Equal(result.RunId, run["runId"]!.GetValue<string>());
        Assert.Equal("investigation", run["kind"]!.GetValue<string>());

        var detail = catalog.Detail(s.Harness.ProjectId, result.RunId);
        Assert.Equal(["frame-0", "gather1-1", "conclude-2", "verify"], detail["steps"]!.AsArray().Select(step => step!["id"]!.GetValue<string>()));
    }

    // ------------------------------------------------------------------ A memória e o laço

    private static JsonArray Retrieved(params (string Id, string Path)[] items) =>
        new(items.Select(item => (JsonNode)new JsonObject { ["id"] = item.Id, ["path"] = item.Path, ["chunkId"] = item.Path + "#h:x" }).ToArray());

    [Fact]
    public async Task O_pedido_leva_a_memoria_so_com_decisoes_e_itens_autorizados_e_num_indice_a_parte()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var s = new Setup(indexRoot: index);
            await s.SeedAsync();
            s.Harness.Project.Write("docs/cobranca.md", "# Cobrança\n\nTexto de um arquivo do projeto.\n");
            var decision = s.RecordDecision("Adiar o marco", "Adiar o marco da migração.", "Bloqueios.");
            s.Runtime.Reply = request => Ack(request);

            await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

            var memory = s.Runtime.Seen!["memory"]!;
            Assert.Equal(index, memory["root"]!.GetValue<string>());
            var paths = memory["documents"]!.AsArray().Select(item => item!["path"]!.GetValue<string>()).ToList();
            Assert.Equal([$"@decision/{decision.Id}.md"], paths);
            Assert.Contains("Decidido: Adiar o marco da migração.", memory["documents"]![0]!["text"]!.GetValue<string>());
            // Arquivo do projeto nunca vai pela memória, e o índice é outro que o de engenharia.
            Assert.DoesNotContain("Texto de um arquivo do projeto", memory.ToJsonString(), StringComparison.Ordinal);
            Assert.True(Directory.Exists(index));
        }
        finally
        {
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    [Fact]
    public async Task Sem_decisao_nem_item_autorizado_ou_sem_indice_o_pedido_nao_leva_memoria()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var empty = new Setup(indexRoot: index);
            await empty.SeedAsync();
            empty.Runtime.Reply = request => Ack(request);
            await empty.Service.InvestigateAsync(empty.Harness.ProjectId, Question, CancellationToken.None);

            using var noIndex = new Setup();
            await noIndex.SeedAsync();
            noIndex.RecordDecision("Adiar", "Adiar.");
            noIndex.Runtime.Reply = request => Ack(request);
            await noIndex.Service.InvestigateAsync(noIndex.Harness.ProjectId, Question, CancellationToken.None);

            Assert.Null(empty.Runtime.Seen!["memory"]);
            Assert.Null(noIndex.Runtime.Seen!["memory"]);
            Assert.False(Directory.Exists(index));
        }
        finally
        {
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    [Fact]
    public async Task O_trecho_da_memoria_vira_fato_com_o_texto_do_documento_do_motor_e_a_afirmacao_que_o_cita_passa()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var s = new Setup(indexRoot: index);
            await s.SeedAsync();
            var decision = s.RecordDecision("Adiar o marco", "Adiar o marco da migração em 2 semanas.", "Bloqueios.");
            var path = $"@decision/{decision.Id}.md";
            var answer = GoodAnswer();
            answer["observed"] = Claims(Claim("A equipe decidiu adiar o marco em 2 semanas.", "memory.1"));
            s.Runtime.Reply = request => Ack(request, answer, retrieved: Retrieved(("memory.1", path)),
                consulted: new JsonArray("today", "flow.issues.items", "risk.overdue", "memory.1"),
                path: GoodPath("today", "memory.1"));

            var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

            var fact = Assert.Single(result.Answer.Evidence, item => item.Id == "memory.1");
            Assert.Equal("memória", fact.Group);
            Assert.StartsWith("Memória do projeto (decisão registrada pela equipe): # Decisão registrada: Adiar o marco", fact.Text);
            Assert.Contains("Decidido: Adiar o marco da migração em 2 semanas.", fact.Text);
            Assert.Equal(["memory.1"], Assert.Single(result.Answer.Observed).Evidence);
            Assert.Contains("memory.1", result.Path[1].Facts);
        }
        finally
        {
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    [Fact]
    public async Task Numero_que_o_documento_da_memoria_nao_tem_e_descartado_como_em_qualquer_fato()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var s = new Setup(indexRoot: index);
            await s.SeedAsync();
            var decision = s.RecordDecision("Adiar o marco", "Adiar o marco da migração em 2 semanas.");
            var answer = GoodAnswer();
            answer["observed"] = Claims(Claim("A equipe decidiu adiar o marco em 9 semanas.", "memory.1"));
            answer["summary"] = Claim("Há uma decisão sobre o marco.", "memory.1");
            answer["inferences"] = new JsonArray();
            s.Runtime.Reply = request => Ack(request, answer, retrieved: Retrieved(("memory.1", $"@decision/{decision.Id}.md")),
                consulted: new JsonArray("memory.1"), path: GoodPath("memory.1"));

            var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

            Assert.Empty(result.Answer.Observed);
            Assert.Contains("9", Assert.Single(result.Answer.Rejected).Reason);
        }
        finally
        {
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    [Fact]
    public async Task Trecho_de_um_documento_que_o_motor_nao_mandou_ou_com_id_inventado_nao_vira_fato()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            using var s = new Setup(indexRoot: index);
            await s.SeedAsync();
            s.RecordDecision("Adiar o marco", "Adiar o marco da migração em 2 semanas.");
            var answer = GoodAnswer();
            answer["observed"] = Claims(Claim("Item inventado.", "memory.1"), Claim("Outro inventado.", "livre"));
            answer["summary"] = Claim("Resumo sem fato valido.", "today");
            answer["inferences"] = new JsonArray();
            s.Runtime.Reply = request => Ack(request, answer,
                retrieved: Retrieved(("memory.1", "@decision/dec-ffffffffffff.md"), ("livre", "@decision/dec-000000000000.md")),
                consulted: new JsonArray("memory.1", "livre"), path: GoodPath("memory.1", "livre"));

            var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

            Assert.Empty(result.Answer.Observed);
            Assert.Equal(2, result.Answer.Rejected.Count);
            Assert.DoesNotContain(result.Path.SelectMany(step => step.Facts), id => id is "memory.1" or "livre");
        }
        finally
        {
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    [Fact]
    public async Task O_caminho_aceita_a_etapa_de_busca_na_memoria()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var path = GoodPath("today");
        path.Insert(2, new JsonObject
        {
            ["id"] = "retrieve", ["title"] = "Memória do projeto consultada (rodada 2)", ["detail"] = "1 busca; 0 trechos.", ["round"] = 4,
            ["groups"] = new JsonArray("memória"), ["facts"] = new JsonArray()
        });
        s.Runtime.Reply = request =>
        {
            var ack = Ack(request, path: path);
            ack["rounds"] = 4;
            return ack;
        };

        var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, Question, CancellationToken.None);

        Assert.Equal(["frame", "gather", "retrieve", "conclude"], result.Path.Select(step => step.Id));
        Assert.Equal(4, result.Rounds);
    }

    // ------------------------------------------------------------------ Com o runtime de verdade

    [Fact]
    public async Task Com_o_runtime_python_de_verdade_e_sem_modelo_lista_os_sinais_e_o_motor_confere()
    {
        var python = new LoopbackAiRuntime();
        try
        {
            using var s = new Setup(modelConfigured: false, runtime: python);
            await s.SeedAsync();
            s.Journal("run-aaaaaaaaaaaa", RunKinds.Workflow, RunStates.Failed, 3);

            var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, "Quais são os principais riscos e execuções com falha?", CancellationToken.None);

            Assert.False(result.UsedModel);
            Assert.Equal(["frame", "gather", "hypothesize", "decide", "conclude"], result.Path.Select(step => step.Id));
            Assert.Contains("INVESTIGATION_NO_MODEL", result.Limitations.Select(item => item.Code));
            Assert.Contains(result.Answer.Observed, item => item.Evidence.Contains("risk.overdue"));
            Assert.Contains(result.Answer.Observed, item => item.Evidence.Contains("runs.failed"));
            Assert.Empty(result.Answer.Rejected);
            Assert.NotEmpty(result.Answer.Unavailable);
            Assert.Contains("risk.overdue", result.Path[1].Facts);
            var journal = s.Store.LoadJournal(s.Harness.ProjectId, result.RunId)!;
            Assert.Equal(RunStates.CompletedWithLimitations, journal.State);
            Assert.Null(journal.ModelUsage);
        }
        finally
        {
            await python.ShutdownAsync();
        }
    }

    private sealed class ConfiguredSettings : IModelSettings
    {
        public ModelStatus Status() => new(true, "openai");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = "sk-test-chave-investigacao-91c2";
            return true;
        }
    }

    private sealed class NoopTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            Task.FromResult(new ModelTransportResult("", 0));
    }

    /// <summary>O provedor roteirizado: uma resposta por chamada, na ordem, e guarda o que recebeu.</summary>
    private sealed class ScriptedProvider(params string[] replies) : IModelChatTransport
    {
        private readonly Queue<string> _replies = new(replies);
        public List<string> Seen { get; } = [];

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            lock (Seen) Seen.Add(string.Join("\n", request.Messages.Select(message => message.Content)));
            var reply = _replies.Count > 0 ? _replies.Dequeue() : "{}";
            return Task.FromResult(new ChatTransportResult(reply, "gpt-4o-mini-2024-07-18", 400, 100));
        }
    }

    [Fact]
    public async Task Com_o_runtime_python_e_um_modelo_roteirizado_o_laco_busca_na_memoria_e_o_motor_confere_a_citacao()
    {
        var index = Path.Combine(Path.GetTempPath(), "inv-memory-" + Guid.NewGuid().ToString("N")[..8]);
        var python = new LoopbackAiRuntime();
        try
        {
            var provider = new ScriptedProvider(
                new JsonObject
                {
                    ["schema"] = "management.hypotheses.v1",
                    ["hypotheses"] = new JsonArray(Claim("A vazão pode estar ligada a um adiamento.", "flow.issues.items")),
                    ["needs"] = new JsonArray(),
                    ["searches"] = new JsonArray("adiar migração faturamento")
                }.ToJsonString(),
                new JsonObject
                {
                    ["schema"] = "management.hypotheses.v1",
                    ["hypotheses"] = new JsonArray(Claim("Já foi decidido adiar a migração.", "memory.1")),
                    ["needs"] = new JsonArray(),
                    ["searches"] = new JsonArray()
                }.ToJsonString(),
                new JsonObject
                {
                    ["schema"] = "management.conclusion.v1",
                    ["summary"] = Claim("A equipe já decidiu adiar a migração.", "memory.1"),
                    ["observed"] = Claims(Claim("Decidido: adiar a migração do faturamento em duas semanas.", "memory.1")),
                    ["calculations"] = new JsonArray(),
                    ["inferences"] = new JsonArray(),
                    ["recommendations"] = new JsonArray(),
                    ["unavailable"] = new JsonArray()
                }.ToJsonString());
            var access = new LocalModelAccess(new ModelBroker(new ModelGateway(new ConfiguredSettings(), new NoopTransport(), chat: provider)));
            using var s = new Setup(runtime: python, indexRoot: index, modelAccess: access);
            await s.SeedAsync();
            s.RecordDecision("Adiar a migração do faturamento", "Adiar a migração do faturamento em duas semanas.", "Bloqueios de infraestrutura.");

            var result = await s.Service.InvestigateAsync(s.Harness.ProjectId, "Por que a vazão caiu depois da migração?", CancellationToken.None);

            Assert.True(result.UsedModel);
            Assert.Equal(["frame", "gather", "hypothesize", "decide", "retrieve", "hypothesize", "decide", "conclude"], result.Path.Select(step => step.Id));
            var fact = Assert.Single(result.Answer.Evidence, item => item.Id == "memory.1");
            Assert.Contains("Adiar a migração do faturamento em duas semanas.", fact.Text);
            Assert.Equal(["memory.1"], Assert.Single(result.Answer.Observed).Evidence);
            Assert.Empty(result.Answer.Rejected);
            Assert.Equal(3, provider.Seen.Count);
            Assert.Contains("[memory.1]", provider.Seen[1]);
            Assert.DoesNotContain("memory.1", provider.Seen[0].Split("<conteudo-do-projeto>")[1]);
            // A consulta é texto do modelo: não vai para o diário.
            var journal = s.Store.LoadJournal(s.Harness.ProjectId, result.RunId)!;
            Assert.DoesNotContain("adiar migração faturamento", System.Text.Json.JsonSerializer.Serialize(journal), StringComparison.Ordinal);
            Assert.Equal(3, journal.ModelUsage!.Calls);
            Assert.NotEmpty(Directory.GetFiles(index, "*", SearchOption.AllDirectories));
        }
        finally
        {
            await python.ShutdownAsync();
            if (Directory.Exists(index)) Directory.Delete(index, true);
        }
    }

    // ------------------------------------------------------------------ Contrato

    [Fact]
    public async Task A_resposta_do_protocolo_segue_o_contrato_que_a_interface_le()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request, rejected: new JsonArray(new JsonObject { ["text"] = "Sem citação.", ["reason"] = "no_valid_evidence" }),
            limitations: new JsonArray(new JsonObject { ["code"] = "INVESTIGATION_STOPPED", ["message"] = "A investigação parou antes de olhar: riscos." }));

        var response = s.Request(ProtocolMethods.ManagementInvestigate, new JsonObject { ["projectId"] = s.Harness.ProjectId, ["question"] = Question });

        var result = response["result"]!;
        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(result["investigationId"]!.GetValue<string>(), "inv-000000000001").Replace(s.Harness.ProjectId, "p-000000000001").ReplaceLineEndings("\n");
        text = Regex.Replace(text, "\"answeredAt\": \"[^\"]*\"", "\"answeredAt\": \"2026-11-10T12:00:00+00:00\"");
        Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", text, StringComparison.Ordinal);

        var path = ContractFiles.Path_("fixtures", "management", "investigation.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "investigation.json: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }

    // ------------------------------------------------------------------ Protocolo

    [Fact]
    public async Task O_metodo_do_protocolo_devolve_o_resultado_e_so_existe_com_o_runtime()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Runtime.Reply = request => Ack(request);

        var response = s.Request(ProtocolMethods.ManagementInvestigate, new JsonObject { ["projectId"] = s.Harness.ProjectId, ["question"] = Question });
        var missing = s.Request(ProtocolMethods.ManagementInvestigate, new JsonObject { ["projectId"] = s.Harness.ProjectId });

        Assert.Equal("Há 2 itens abertos.", response["result"]!["answer"]!["summary"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, missing["error"]!["code"]!.GetValue<string>());
        Assert.Contains(ProtocolMethods.ManagementInvestigate, ProtocolMethods.Management);

        using var bare = new Setup(withRuntime: false);
        await bare.SeedAsync();
        var unavailable = bare.Request(ProtocolMethods.ManagementInvestigate, new JsonObject { ["projectId"] = bare.Harness.ProjectId, ["question"] = Question });
        Assert.Equal("INVESTIGATION_UNAVAILABLE", unavailable["error"]!["code"]!.GetValue<string>());
    }
}
