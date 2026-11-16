using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Timeline;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Um acontecimento de cada fonte, em dias diferentes.
public class ProjectTimelineTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private const string ItemId = "acme/app#12";

    private sealed class World : IDisposable
    {
        public IntegrationHarness H { get; } = new();
        public FileRunStore Store { get; }

        public World() => Store = new FileRunStore(H.DataDir);

        public void At(int day, int hour = 12) => H.Clock.UtcNow = new DateTimeOffset(2026, 11, day, hour, 0, 0, TimeSpan.Zero);

        public RunJournal Journal(string runId, string kind, string state, DateTimeOffset started, Action<RunJournal>? shape = null)
        {
            var journal = new RunJournal { RunId = runId, ProjectId = H.ProjectId, Kind = kind, State = state, StartedAt = started, FinishedAt = started.AddMinutes(2) };
            shape?.Invoke(journal);
            Store.SaveJournal(journal);
            return journal;
        }

        public ProjectTimelineService Service(
            bool revisions = true, bool profiles = true, bool decisions = true, bool management = true, bool integrations = true,
            IDecisionStore? decisionStore = null)
        {
            var decisionService = new DecisionService(H.Workspace, decisionStore ?? new FileDecisionStore(H.DataDir), Store,
                id => new EvidencePack(id, "Loja", H.Clock.UtcNow, [new EvidenceFact("today", "contexto", "Hoje é um dia.")], []), H.Audit, H.Clock);
            return new ProjectTimelineService(
                H.Workspace, H.Clock, Store,
                revisions ? new HarnessRevisionService(H.Workspace, new TestFilesFactory(), Store) : null,
                profiles ? new TechnologyProfileService(null!, H.Hub, H.Workspace, new FileTechnologyProfileStore(H.DataDir), H.Clock) : null,
                decisions ? decisionService : null,
                management ? new ManagementService(H.Workspace, H.Hub, H.Clock, new FileRiskHistoryStore(H.DataDir)) : null,
                integrations ? H.Hub : null);
        }

        public DecisionService Decisions() => new(H.Workspace, new FileDecisionStore(H.DataDir), Store,
            id => new EvidencePack(id, "Loja", H.Clock.UtcNow, [new EvidenceFact("today", "contexto", "Hoje é um dia.")], []), H.Audit, H.Clock);

        public void Dispose() => H.Dispose();
    }

    private static JournalOp Op(string path, string op) => new() { Path = path, Op = op, Status = OpStatus.Applied };

    private static async Task<World> Populated()
    {
        var w = new World();

        // Dia 1: o Harness é aplicado (vira a revisão 1).
        w.Journal("ex-000000000001", RunKinds.Apply, RunStates.Completed, Day1, journal =>
        {
            journal.Ops.Add(Op(".claude/agents/code-reviewer.md", OpKinds.Create));
            journal.Ops.Add(Op(".claude/rules/general.md", OpKinds.Create));
        });
        // Dia 2: uma validação, com 2 de 3 verificações.
        w.Journal("ex-000000000002", RunKinds.Validate, RunStates.CompletedWithLimitations, Day1.AddDays(1), journal =>
        {
            journal.Evals.Add(new EvalSummary { Id = "a", Status = "Passed", Required = true });
            journal.Evals.Add(new EvalSummary { Id = "b", Status = "Passed", Required = false });
            journal.Evals.Add(new EvalSummary { Id = "c", Status = "Failed", Required = false });
        });
        // Dia 3: um pedido de engenharia que falhou.
        w.Journal("ex-000000000003", RunKinds.Workflow, RunStates.Failed, Day1.AddDays(2));

        // Dia 4: o perfil muda.
        var profile = new TechnologyProfile(w.H.ProjectId, Day1.AddDays(3), [], [], [], [], [],
        [
            new ProfileChange(Day1.AddDays(3), "changed", "Next.js", "16.0.1", "16.1.0"),
            new ProfileChange(Day1.AddDays(3), "added", "Vitest", null, "3.2.0"),
            new ProfileChange(Day1.AddDays(3), "removed", "Jest", null, null)
        ]);
        new FileTechnologyProfileStore(w.H.DataDir).Write(profile);

        // Dia 5: uma sincronização do GitHub.
        w.H.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-04T10:00:00Z"));
        w.At(5);
        var integration = await w.H.LinkGitHubAsync();
        await w.H.SyncAsync(integration);

        // Dia 6: os riscos mudam (aparece, sobe, some).
        var history = new FileRiskHistoryStore(w.H.DataDir);
        history.Save(w.H.ProjectId,
        [
            new RiskSnapshot(Day1.AddDays(5), Day1.AddDays(5), [new RiskSnapshotEntry("stalled", "Itens parados", "low", [ItemId])]),
            new RiskSnapshot(Day1.AddDays(6), Day1.AddDays(6), [new RiskSnapshotEntry("stalled", "Itens parados", "medium", [ItemId, "acme/app#13"])]),
            new RiskSnapshot(Day1.AddDays(7), Day1.AddDays(7), [])
        ]);

        // Dia 9: uma decisão gerencial.
        w.At(9);
        w.Decisions().Record(w.H.ProjectId, "Adiar o marco em duas semanas", "Adiar o marco porque o ritmo caiu depois da mudança.", "Texto longo da justificativa.", []);
        w.At(10);
        return w;
    }

    private static IReadOnlyList<TimelineEvent> Events(ProjectTimeline timeline, string kind) => timeline.Events.Where(item => item.Kind == kind).ToList();

    [Fact]
    public async Task Cada_fonte_traz_os_seus_acontecimentos_do_mais_novo_ao_mais_antigo()
    {
        using var w = await Populated();

        var timeline = w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.Equal(["decision", "risk", "risk", "risk", "sync", "profile", "profile", "profile", "run", "run", "harness"],
            timeline.Events.Select(item => item.Kind));
        Assert.Equal(timeline.Events.OrderByDescending(item => item.At).Select(item => item.Id), timeline.Events.Select(item => item.Id));
        Assert.Equal(["decision", "harness", "profile", "risk", "run", "sync"], timeline.Sources.Select(source => source.Kind).Order());
        Assert.All(timeline.Sources, source => Assert.True(source.Available));
        Assert.Equal(11, timeline.Events.Count);
        Assert.Null(timeline.NextBefore);
    }

    [Fact]
    public async Task A_revisao_do_harness_diz_o_que_mudou_e_aponta_para_a_execucao()
    {
        using var w = await Populated();

        var revision = Assert.Single(Events(w.Service().Read(w.H.ProjectId, [], 200, null), "harness"));

        Assert.Equal("Revisão 1 do Harness", revision.Title);
        Assert.Contains("2 arquivos criados", revision.Detail);
        Assert.Contains("Agente revisor de código", revision.Detail);
        Assert.Equal(("run", "ex-000000000001", "ok"), (revision.RefType, revision.RefId, revision.Level));
    }

    [Fact]
    public async Task Aplicacao_sem_mudanca_de_arquivo_nao_vira_revisao_mas_aparece_como_execucao_e_nada_aparece_duas_vezes()
    {
        using var w = await Populated();
        w.Journal("ex-000000000004", RunKinds.Apply, RunStates.Failed, Day1.AddDays(3).AddHours(2));

        var timeline = w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.Single(Events(timeline, "harness"));
        var failed = Events(timeline, "run").Single(item => item.RefId == "ex-000000000004");
        Assert.Equal(("Aplicação do Harness", "problem"), (failed.Title, failed.Level));
        Assert.Equal(timeline.Events.Count, timeline.Events.Select(item => item.Id).Distinct().Count());
        Assert.DoesNotContain(Events(timeline, "run"), item => item.RefId == "ex-000000000001");
    }

    [Fact]
    public async Task As_execucoes_dizem_o_tipo_o_estado_e_as_verificacoes_sem_nenhum_texto_de_pedido()
    {
        using var w = await Populated();

        var runs = Events(w.Service().Read(w.H.ProjectId, [], 200, null), "run");
        var workflow = runs.Single(item => item.Title == "Pedido de engenharia");
        var validation = runs.Single(item => item.Title == "Validação do Harness");

        Assert.Equal(("problem", "Falhou."), (workflow.Level, workflow.Detail));
        Assert.Equal("attention", validation.Level);
        Assert.Equal("Concluída com limitações; 2 de 3 verificações passaram.", validation.Detail);
    }

    [Fact]
    public async Task Limitacoes_de_um_pedido_nao_contam_como_verificacoes_que_falharam()
    {
        using var w = new World();
        w.Journal("ex-000000000009", RunKinds.Workflow, RunStates.CompletedWithLimitations, Day1, journal =>
        {
            journal.Evals.Add(new EvalSummary { Id = "INDEX_PENDING", Status = "recorded", Required = false });
            journal.Evals.Add(new EvalSummary { Id = "EMBEDDINGS_NOT_READY", Status = "recorded", Required = false });
            journal.Evals.Add(new EvalSummary { Id = "JUDGE_NOT_RUN", Status = "not_run", Required = false });
        });

        var run = Assert.Single(Events(await Task.FromResult(w.Service().Read(w.H.ProjectId, [], 200, null)), "run"));

        Assert.Equal("Concluída com limitações; 1 verificação não executada.", run.Detail);
    }

    [Fact]
    public async Task As_mudancas_do_perfil_viram_frases_simples()
    {
        using var w = await Populated();

        var titles = Events(w.Service().Read(w.H.ProjectId, [], 200, null), "profile").Select(item => item.Title).Order().ToList();

        Assert.Equal(["Jest saiu do perfil", "Next.js mudou de 16.0.1 para 16.1.0", "Vitest entrou no perfil (3.2.0)"], titles);
    }

    [Fact]
    public async Task A_decisao_traz_o_titulo_e_quantos_fatos_do_momento_e_nunca_o_texto_da_decisao_nem_a_justificativa()
    {
        using var w = await Populated();

        var timeline = w.Service().Read(w.H.ProjectId, [], 200, null);
        var decision = Assert.Single(Events(timeline, "decision"));

        Assert.Equal("Decisão: Adiar o marco em duas semanas", decision.Title);
        Assert.Equal("Registrada com 1 fato do momento.", decision.Detail);
        Assert.Equal("decision", decision.RefType);
        var everything = JsonSerializer.Serialize(timeline, IntegrationJson.Options);
        Assert.DoesNotContain("porque o ritmo caiu", everything);
        Assert.DoesNotContain("Texto longo da justificativa", everything);
    }

    [Fact]
    public async Task As_mudancas_de_risco_dizem_o_que_aconteceu_e_so_contam_os_itens_sem_citar_nenhum()
    {
        using var w = await Populated();

        var timeline = w.Service().Read(w.H.ProjectId, [], 200, null);
        var risks = Events(timeline, "risk").OrderBy(item => item.At).ToList();

        Assert.Equal(
            ["Risco \"Itens parados\": já estava ativo no primeiro registro, com gravidade baixa", "Risco \"Itens parados\": subiu de gravidade baixa para média", "Risco \"Itens parados\": deixou de existir"],
            risks.Select(item => item.Title));
        Assert.Equal(["info", "attention", "ok"], risks.Select(item => item.Level));
        Assert.DoesNotContain("acme/app#", JsonSerializer.Serialize(timeline, IntegrationJson.Options));
    }

    [Fact]
    public async Task A_sincronizacao_concluida_traz_o_provedor_o_recurso_e_as_contagens()
    {
        using var w = await Populated();

        var sync = Assert.Single(Events(w.Service().Read(w.H.ProjectId, [], 200, null), "sync"));

        Assert.Equal("Sincronização de GitHub acme/app", sync.Title);
        Assert.Equal("Concluída; 1 novo, 0 atualizados, 0 sem mudança.", sync.Detail);
        Assert.Equal("ok", sync.Level);
    }

    [Fact]
    public async Task O_filtro_por_tipo_mostra_so_aquele_tipo_mas_a_contagem_das_fontes_continua_inteira()
    {
        using var w = await Populated();

        var timeline = w.Service().Read(w.H.ProjectId, ["risk", "decision"], 200, null);

        Assert.All(timeline.Events, item => Assert.Contains(item.Kind, new[] { "risk", "decision" }));
        Assert.Equal(4, timeline.Events.Count);
        Assert.Equal(3, timeline.Sources.Single(source => source.Kind == "profile").Count);
    }

    [Fact]
    public async Task Tipo_que_nao_existe_e_recusado()
    {
        using var w = await Populated();

        var error = Assert.Throws<EngineException>(() => w.Service().Read(w.H.ProjectId, ["inventado"], null, null));

        Assert.Equal("TIMELINE_KIND_UNKNOWN", error.Code);
    }

    [Fact]
    public async Task A_pagina_segue_pelo_instante_e_juntas_cobrem_tudo_sem_repetir_nem_perder()
    {
        using var w = await Populated();
        var service = w.Service();
        var everything = service.Read(w.H.ProjectId, [], 200, null).Events.Select(item => item.Id).ToList();

        var collected = new List<string>();
        DateTimeOffset? before = null;
        var pages = 0;
        do
        {
            var page = service.Read(w.H.ProjectId, [], 3, before);
            collected.AddRange(page.Events.Select(item => item.Id));
            before = page.NextBefore;
            pages++;
        } while (before is not null && pages < 20);

        Assert.Equal(everything, collected);
        Assert.True(pages > 1);
    }

    [Fact]
    public async Task Acontecimentos_com_o_mesmo_instante_na_fronteira_ficam_na_mesma_pagina()
    {
        using var w = await Populated();
        // Os três do perfil têm o mesmo instante: uma página de 1 elemento no meio deles não pode separá-los.
        var service = w.Service();
        var all = service.Read(w.H.ProjectId, [], 200, null).Events;
        var firstProfile = all.Select((item, index) => (item, index)).First(pair => pair.item.Kind == "profile").index;

        var page = service.Read(w.H.ProjectId, [], firstProfile + 1, null);

        Assert.Equal(firstProfile + 3, page.Events.Count);
        Assert.Equal(all.Take(firstProfile + 3).Select(item => item.Id), page.Events.Select(item => item.Id));
    }

    [Fact]
    public async Task O_limite_tem_teto_e_piso()
    {
        using var w = await Populated();
        var service = w.Service();

        Assert.Single(service.Read(w.H.ProjectId, [], 0, null).Events.Take(1));
        Assert.True(service.Read(w.H.ProjectId, [], 10_000, null).Events.Count <= ProjectTimelineService.MaxLimit);
    }

    private sealed class BrokenDecisions : IDecisionStore
    {
        public IReadOnlyList<ManagementDecision> List(string projectId) => throw new EngineException("DECISION_STORE_BROKEN", "disco com defeito: C:\\segredo\\caminho");
        public ManagementDecision? Get(string projectId, string decisionId) => null;
        public void Save(ManagementDecision decision) { }
    }

    [Fact]
    public async Task Uma_fonte_que_falha_aparece_como_indisponivel_e_as_outras_seguem_sem_vazar_o_motivo()
    {
        using var w = await Populated();

        var timeline = w.Service(decisionStore: new BrokenDecisions()).Read(w.H.ProjectId, [], 200, null);

        var decisions = timeline.Sources.Single(source => source.Kind == "decision");
        Assert.Equal((false, 0), (decisions.Available, decisions.Count));
        Assert.Equal(10, timeline.Events.Count);
        Assert.DoesNotContain("segredo", JsonSerializer.Serialize(timeline, IntegrationJson.Options));
    }

    [Fact]
    public async Task Sem_o_servico_de_uma_fonte_ela_nem_existe_e_sem_revisoes_a_aplicacao_aparece_como_execucao()
    {
        using var w = await Populated();

        var timeline = w.Service(revisions: false, integrations: false, management: false).Read(w.H.ProjectId, [], 200, null);

        Assert.DoesNotContain(timeline.Sources, source => source.Kind is "harness" or "sync" or "risk");
        Assert.Contains(Events(timeline, "run"), item => item.RefId == "ex-000000000001");
    }

    [Fact]
    public async Task Projeto_nao_autorizado_e_recusado_e_o_de_outro_projeto_nao_aparece()
    {
        using var w = await Populated();
        w.Store.SaveJournal(new RunJournal
        {
            RunId = "ex-0000000000ff", ProjectId = w.H.OtherProjectId, Kind = RunKinds.Workflow, State = RunStates.Completed, StartedAt = Day1, FinishedAt = Day1
        });
        var service = w.Service();

        var error = Assert.Throws<EngineException>(() => service.Read("p-desconhecido01", [], null, null));
        var mine = service.Read(w.H.ProjectId, [], 200, null);

        Assert.Equal("PROJECT_NOT_AUTHORIZED", error.Code);
        Assert.DoesNotContain(mine.Events, item => item.RefId == "ex-0000000000ff");
        Assert.Contains(service.Read(w.H.OtherProjectId, [], 200, null).Events, item => item.RefId == "ex-0000000000ff");
    }

    [Fact]
    public async Task Ler_a_linha_do_tempo_nao_grava_nada()
    {
        using var w = await Populated();
        var before = w.H.EverythingOnDisk();

        w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.Equal(before, w.H.EverythingOnDisk());
    }

    [Fact]
    public async Task Nenhum_texto_tem_travessao_e_nada_leva_credencial()
    {
        using var w = await Populated();

        var text = JsonSerializer.Serialize(w.Service().Read(w.H.ProjectId, [], 200, null), IntegrationJson.Options);

        Assert.DoesNotContain("\u2014", text);
        Assert.DoesNotContain(FakeGitHub.Token, text);
    }

    private static JsonObject Call(EngineSession session, JsonObject parameters) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "timeline.read", ["params"] = parameters }.ToJsonString()).Response;

    [Fact]
    public async Task O_metodo_responde_filtra_pagina_e_recusa_o_que_nao_entende()
    {
        using var w = await Populated();
        var session = new EngineSession("test", timeline: w.Service());
        var project = new JsonObject { ["projectId"] = w.H.ProjectId };

        var all = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["limit"] = 4 });
        var next = all["result"]!["nextBefore"]!.GetValue<string>();
        var older = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["limit"] = 4, ["before"] = next });
        var runs = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["kinds"] = new JsonArray("run") });
        var badKind = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["kinds"] = new JsonArray("inventado") });
        var badBefore = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["before"] = "ontem" });
        var unknownProject = Call(session, new JsonObject { ["projectId"] = "p-desconhecido01" });

        Assert.Equal(4, all["result"]!["events"]!.AsArray().Count);
        Assert.True(older["result"]!["events"]!.AsArray().All(item => DateTimeOffset.Parse(item!["at"]!.GetValue<string>()) < DateTimeOffset.Parse(next)));
        Assert.Equal(2, runs["result"]!["events"]!.AsArray().Count);
        Assert.Equal("TIMELINE_KIND_UNKNOWN", badKind["error"]!["code"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, badBefore["error"]!["code"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", unknownProject["error"]!["code"]!.GetValue<string>());
        Assert.NotNull(project);
    }

    [Fact]
    public async Task Fixture_de_contrato_da_linha_do_tempo()
    {
        using var w = await Populated();
        var timeline = w.Service().Read(w.H.ProjectId, [], 200, null);

        var text = JsonSerializer.Serialize(timeline, IntegrationJson.Options).Replace(w.H.ProjectId, "p-000000000001");
        text = Regex.Replace(text, "(dec|sync)-[0-9a-f]{12}", match => match.Groups[1].Value + "-000000000001");
        // A hora em que a leitura foi feita é a do relógio de teste: fixa. O que muda por execução é só o id de decisão e de sincronização.
        var pretty = JsonNode.Parse(text)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "timeline", "timeline.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "timeline: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
