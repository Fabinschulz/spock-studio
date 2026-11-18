using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Audit;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Os testes usam eventos reais do hub e diários montados à mão.
public class ProjectAuditTests
{
    private const string DecisionText = "Adiar o marco porque o ritmo caiu depois da mudança.";
    private const string Rationale = "Justificativa longa e pessoal da decisão.";

    private sealed class World : IDisposable
    {
        public IntegrationHarness H { get; } = new();
        public FileRunStore Store { get; }
        public ProjectIntegration Integration { get; set; } = null!;
        public IntegrationConnection Connection { get; set; } = null!;

        public World() => Store = new FileRunStore(H.DataDir);

        public void At(int day, int hour = 12) => H.Clock.UtcNow = new DateTimeOffset(2026, 11, day, hour, 0, 0, TimeSpan.Zero);

        public ProjectAuditService Service(IAuditReader? reader = null, bool withReader = true) =>
            new(H.Workspace, H.Clock, Store, withReader ? reader ?? H.Audit : null, H.Hub);

        public void Journal(string runId, string kind, string state, DateTimeOffset started, Action<RunJournal>? shape = null, string? projectId = null)
        {
            var journal = new RunJournal { RunId = runId, ProjectId = projectId ?? H.ProjectId, Kind = kind, State = state, StartedAt = started, FinishedAt = started.AddMinutes(2) };
            shape?.Invoke(journal);
            Store.SaveJournal(journal);
        }

        public void Dispose() => H.Dispose();
    }

    private static async Task<World> Lived()
    {
        var w = new World();
        w.H.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-11-02T10:00:00Z"));
        w.H.GitHub.Issues.Add(FakeGitHub.Issue(2, "2026-11-03T10:00:00Z", pullRequest: true));

        w.At(1);
        w.Connection = await w.H.ConnectGitHubAsync("GitHub da equipe");
        await w.H.ConnectGitHubAsync("Outro GitHub");
        w.At(2);
        w.Integration = await w.H.Hub.Projects.LinkAsync(w.H.ProjectId, w.Connection.Id, FakeGitHub.Repository, [DataKinds.Issues, DataKinds.PullRequests], CancellationToken.None);
        w.At(3);
        await w.H.SyncAsync(w.Integration);
        w.At(4);
        w.H.Hub.Projects.Configure(w.H.ProjectId, w.Integration.Id, null, true);
        w.At(5);
        w.H.Hub.Projects.Configure(w.H.ProjectId, w.Integration.Id, [DataKinds.Issues], null);
        w.At(6);
        w.H.Hub.Projects.Configure(w.H.ProjectId, w.Integration.Id, null, true);
        w.At(7);
        new DecisionService(w.H.Workspace, new FileDecisionStore(w.H.DataDir), w.Store,
            id => new EvidencePack(id, "Loja", w.H.Clock.UtcNow, [new EvidenceFact("today", "contexto", "Hoje é um dia.")], []), w.H.Audit, w.H.Clock)
            .Record(w.H.ProjectId, "Adiar o marco", DecisionText, Rationale, []);
        w.At(8);
        await w.H.Hub.Connections.RotateAsync(w.Connection.Id, FakeGitHub.Token, CancellationToken.None);
        w.At(10);
        w.H.Hub.Projects.Unlink(w.H.ProjectId, w.Integration.Id);

        // O diário: a aplicação do Harness, uma reversão, uma aplicação desfeita na hora e um pedido de engenharia com ações aprovadas.
        var day1 = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        w.Journal("ex-000000000001", RunKinds.Apply, RunStates.Completed, day1, journal =>
        {
            journal.Ops.Add(new JournalOp { Path = ".claude/rules/general.md", Op = OpKinds.Create, Status = OpStatus.Applied });
            journal.Ops.Add(new JournalOp { Path = ".claude/agents/code-reviewer.md", Op = OpKinds.Create, Status = OpStatus.Applied });
        });
        w.Journal("ex-000000000002", RunKinds.Revert, RunStates.Completed, day1.AddDays(1), journal =>
            journal.Ops.Add(new JournalOp { Path = ".claude/rules/general.md", Op = OpKinds.Create, Status = OpStatus.Reverted }));
        w.Journal("ex-000000000003", RunKinds.Apply, RunStates.Failed, day1.AddDays(2), journal =>
        {
            journal.RolledBack = true;
            journal.Ops.Add(new JournalOp { Path = ".claude/rules/x.md", Op = OpKinds.Create, Status = OpStatus.Applied });
        });
        w.Journal("ex-000000000004", RunKinds.Workflow, RunStates.Completed, day1.AddDays(3), journal =>
        {
            journal.ApprovedAt = day1.AddDays(3).AddMinutes(1);
            journal.ApprovalId = "ap-000000000001";
            journal.Actions.Add(new JournalAction { Id = "a1", Kind = "write", Summary = "Registrar a decisão de arquitetura", Target = ".spock/decisions/ex-000000000004-impacto.md", Status = JournalAction.Executed });
            journal.Actions.Add(new JournalAction { Id = "a2", Kind = "write", Summary = "Outra ação", Target = "docs/x.md", Status = JournalAction.Skipped });
            journal.ModelUsage = new JournalModelUsage { Calls = 3, InputTokens = 1200, OutputTokens = 300 };
        });
        w.At(11);
        return w;
    }

    private static IReadOnlyList<AuditEntry> Of(ProjectAuditTrail trail, string category) => trail.Entries.Where(entry => entry.Category == category).ToList();

    [Fact]
    public async Task O_vinculo_a_politica_o_recorte_a_leitura_a_decisao_e_o_desvinculo_viram_entradas_com_a_linha_do_arquivo()
    {
        using var w = await Lived();

        var trail = w.Service().Read(w.H.ProjectId, [], 200, null);
        var titles = trail.Entries.Where(entry => entry.Source == "audit").OrderBy(entry => entry.AuditLine).Select(entry => entry.Title).ToList();

        Assert.Equal(
            [
                "Conexão criada: GitHub da equipe",
                "Fonte vinculada ao projeto: acme/app",
                "Leitura de acme/app",
                "Política de contexto de IA de acme/app: de fechada para aberta",
                "Recorte de acme/app alterado: de issues, pull requests para issues",
                "Recorte e política de acme/app salvos sem mudança",
                "Decisão gerencial registrada",
                "Credencial atualizada: GitHub da equipe",
                "Fonte desvinculada do projeto: acme/app"
            ],
            titles);
        Assert.All(trail.Entries.Where(entry => entry.Source == "audit"), entry => Assert.True(entry.AuditLine >= 1 && entry.RunId is null));
    }

    [Fact]
    public async Task A_mudanca_de_politica_diz_de_que_para_que_e_o_que_passa_a_valer()
    {
        using var w = await Lived();

        var entry = Assert.Single(Of(w.Service().Read(w.H.ProjectId, [], 200, null), "context"));

        Assert.Equal("Política de contexto de IA de acme/app: de fechada para aberta", entry.Title);
        Assert.Equal("Os itens sincronizados passam a poder virar contexto dos agentes.", entry.Detail);
    }

    [Fact]
    public async Task O_vinculo_nasce_com_a_politica_fechada_e_a_trilha_diz_isso()
    {
        using var w = await Lived();

        var linked = w.Service().Read(w.H.ProjectId, [], 200, null).Entries.Single(entry => entry.Title.StartsWith("Fonte vinculada", StringComparison.Ordinal));

        Assert.Equal("Dados lidos: issues, pull requests. Política de contexto de IA: fechada.", linked.Detail);
    }

    [Fact]
    public async Task A_leitura_diz_de_qual_fonte_foi_o_resultado_e_quantos_itens()
    {
        using var w = await Lived();

        var read = Assert.Single(Of(w.Service().Read(w.H.ProjectId, [], 200, null), "read"));

        Assert.Equal(("Leitura de acme/app", "Concluída; 2 itens lidos."), (read.Title, read.Detail));
    }

    [Fact]
    public async Task O_aviso_por_prompt_diz_o_que_motivou_sem_o_texto_do_prompt()
    {
        using var w = await Lived();
        w.H.Audit.Record("ide.prompt", new System.Text.Json.Nodes.JsonObject
        {
            ["projectId"] = w.H.ProjectId,
            ["contextVersion"] = "ctx-abc",
            ["signals"] = new System.Text.Json.Nodes.JsonArray("version_changed", "stale:pi-1", "stale:pi-2", "hint:infrastructure")
        });

        var entry = w.Service().Read(w.H.ProjectId, [], 200, null).Entries.Single(e => e.Title == "Aviso entregue à IDE pelo hook por prompt");

        Assert.Equal("context", entry.Category);
        Assert.Equal("Versão ctx-abc: o contexto mudou desde o começo da sessão; fonte desatualizada; sugestão de ferramenta para infraestrutura.", entry.Detail);
    }

    [Fact]
    public async Task So_as_conexoes_que_o_projeto_usa_aparecem_e_a_outra_conexao_nao()
    {
        using var w = await Lived();

        var credentials = Of(w.Service().Read(w.H.ProjectId, [], 200, null), "credential").Select(entry => entry.Title).ToList();

        Assert.Equal(["Conexão criada: GitHub da equipe", "Credencial atualizada: GitHub da equipe"], credentials.Order());
        Assert.DoesNotContain(credentials, title => title.Contains("Outro GitHub", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_decisao_so_traz_contagens_nunca_o_texto_nem_a_justificativa()
    {
        using var w = await Lived();

        var trail = w.Service().Read(w.H.ProjectId, [], 200, null);
        var decision = Assert.Single(Of(trail, "decision"));

        Assert.Equal("1 fato do momento e 0 execuções relacionadas.", decision.Detail);
        var everything = JsonSerializer.Serialize(trail, IntegrationJson.Options);
        Assert.DoesNotContain("ritmo caiu", everything);
        Assert.DoesNotContain("Justificativa longa", everything);
    }

    [Fact]
    public async Task O_diario_traz_a_aplicacao_a_reversao_e_a_aplicacao_desfeita_na_hora_com_a_execucao()
    {
        using var w = await Lived();

        var changes = Of(w.Service().Read(w.H.ProjectId, [], 200, null), "change").Where(entry => entry.Source == "journal" && entry.Title.Contains("Harness", StringComparison.Ordinal)).ToList();

        Assert.Equal(
            [("ex-000000000003", "Aplicação do Harness desfeita na hora"), ("ex-000000000002", "Aplicação do Harness revertida"), ("ex-000000000001", "Harness aplicado ao projeto")],
            changes.OrderByDescending(entry => entry.At).Select(entry => (entry.RunId!, entry.Title)));
        Assert.Equal("2 arquivos gravados.", changes.Single(entry => entry.RunId == "ex-000000000001").Detail);
        Assert.Equal("1 arquivo restaurado ao que era antes.", changes.Single(entry => entry.RunId == "ex-000000000002").Detail);
        Assert.All(changes, entry => Assert.True(entry.AuditLine is null && entry.RunId is not null));
    }

    // Validação real B1: recusar tudo passava pela mesma aprovação e aparecia como "aprovadas", com "0 executadas".
    [Fact]
    public void Recusar_todas_as_acoes_aparece_como_recusa_e_aprovar_parte_diz_quantas()
    {
        using var w = new World();
        w.At(10);
        var at = w.H.Clock.UtcNow;
        w.Journal("ex-000000000005", RunKinds.Workflow, RunStates.Completed, at, journal =>
        {
            journal.ApprovedAt = at.AddMinutes(1);
            journal.ApprovedActions = 0;
            journal.Actions.Add(new JournalAction { Id = "a1", Kind = "write", Summary = "Registrar", Target = "docs/a.md", Status = JournalAction.Skipped });
            journal.Actions.Add(new JournalAction { Id = "a2", Kind = "write", Summary = "Outra", Target = "docs/b.md", Status = JournalAction.Skipped });
        });
        w.Journal("ex-000000000006", RunKinds.Workflow, RunStates.Failed, at, journal =>
        {
            // Aprovada e não executada porque o workflow falhou depois: SkipPending não pode transformar isso em recusa.
            journal.ApprovedAt = at.AddMinutes(1);
            journal.ApprovedActions = 1;
            journal.Actions.Add(new JournalAction { Id = "a1", Kind = "write", Summary = "Registrar", Target = "docs/a.md", Status = JournalAction.Skipped });
        });

        var changes = Of(w.Service().Read(w.H.ProjectId, [], 200, null), "change").ToList();

        var rejected = Assert.Single(changes, entry => entry.RunId == "ex-000000000005");
        Assert.Equal(("Ações propostas por agentes recusadas", "Nenhuma das 2 ações propostas foi aprovada; nada foi executado."), (rejected.Title, rejected.Detail));
        var failed = Assert.Single(changes, entry => entry.RunId == "ex-000000000006");
        Assert.Equal(("Ações propostas por agentes aprovadas", "1 ação aprovada; 0 executadas."), (failed.Title, failed.Detail));
    }

    [Fact]
    public async Task As_acoes_de_agentes_aprovadas_e_executadas_aparecem_so_as_executadas_com_o_destino()
    {
        using var w = await Lived();

        var changes = Of(w.Service().Read(w.H.ProjectId, [], 200, null), "change").Where(entry => entry.RunId == "ex-000000000004").ToList();

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, entry => entry.Title == "Ações propostas por agentes aprovadas" && entry.Detail == "1 de 2 ações aprovadas; 1 executada.");
        var executed = Assert.Single(changes, entry => entry.Title.StartsWith("Ação de agente executada", StringComparison.Ordinal));
        Assert.Equal("Destino: .spock/decisions/ex-000000000004-impacto.md.", executed.Detail);
        Assert.DoesNotContain(changes, entry => entry.Title.Contains("Outra ação", StringComparison.Ordinal));
    }

    [Fact]
    public async Task O_uso_de_modelo_diz_chamadas_e_tokens_como_o_proxy_contou()
    {
        using var w = await Lived();

        var usage = Assert.Single(Of(w.Service().Read(w.H.ProjectId, [], 200, null), "model"));

        Assert.Equal(("ex-000000000004", "3 chamadas ao provedor, 1500 tokens (1200 de entrada, 300 de saída)."), (usage.RunId, usage.Detail));
    }

    [Fact]
    public async Task Validacao_e_pergunta_sem_efeito_no_projeto_nao_entram_na_auditoria()
    {
        using var w = await Lived();
        w.Journal("ex-0000000000a1", RunKinds.Validate, RunStates.Completed, new DateTimeOffset(2026, 11, 5, 9, 0, 0, TimeSpan.Zero));

        var trail = w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.DoesNotContain(trail.Entries, entry => entry.RunId == "ex-0000000000a1");
    }

    [Fact]
    public async Task Toda_entrada_tem_exatamente_uma_referencia_e_um_id_unico_e_a_ordem_e_do_mais_novo_ao_mais_antigo()
    {
        using var w = await Lived();

        var trail = w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.All(trail.Entries, entry => Assert.True((entry.RunId is not null) ^ (entry.AuditLine is not null)));
        Assert.Equal(trail.Entries.Count, trail.Entries.Select(entry => entry.Id).Distinct().Count());
        Assert.Equal(trail.Entries.OrderByDescending(entry => entry.At).Select(entry => entry.At), trail.Entries.Select(entry => entry.At));
        Assert.Equal(["audit", "journal"], trail.Sources.Select(source => source.Source).Order());
        Assert.All(trail.Sources, source => Assert.True(source.Available));
    }

    [Fact]
    public async Task As_categorias_trazem_a_contagem_da_trilha_inteira_mesmo_com_filtro()
    {
        using var w = await Lived();

        var filtered = w.Service().Read(w.H.ProjectId, ["decision"], 200, null);
        var counts = filtered.Categories.ToDictionary(category => category.Id, category => category.Count);

        Assert.Equal(["access", "context", "credential", "read", "decision", "change", "model"], filtered.Categories.Select(category => category.Id));
        Assert.Equal((4, 1, 2, 1, 1, 5, 1), (counts["access"], counts["context"], counts["credential"], counts["read"], counts["decision"], counts["change"], counts["model"]));
        Assert.Single(filtered.Entries);
        Assert.Equal(15, counts.Values.Sum());
    }

    [Fact]
    public async Task O_filtro_por_categoria_mostra_so_ela_e_categoria_inexistente_e_recusada()
    {
        using var w = await Lived();

        var only = w.Service().Read(w.H.ProjectId, ["context", "decision"], 200, null);
        var error = Assert.Throws<EngineException>(() => w.Service().Read(w.H.ProjectId, ["inventada"], null, null));

        Assert.Equal(2, only.Entries.Count);
        Assert.All(only.Entries, entry => Assert.Contains(entry.Category, new[] { "context", "decision" }));
        Assert.Equal("AUDIT_CATEGORY_UNKNOWN", error.Code);
    }

    [Fact]
    public async Task A_pagina_segue_pelo_instante_e_juntas_cobrem_tudo_sem_repetir_nem_perder()
    {
        using var w = await Lived();
        var service = w.Service();
        var everything = service.Read(w.H.ProjectId, [], 200, null).Entries.Select(entry => entry.Id).ToList();

        var collected = new List<string>();
        DateTimeOffset? before = null;
        var pages = 0;
        do
        {
            var page = service.Read(w.H.ProjectId, [], 4, before);
            collected.AddRange(page.Entries.Select(entry => entry.Id));
            before = page.NextBefore;
            pages++;
        } while (before is not null && pages < 30);

        Assert.Equal(everything, collected);
        Assert.True(pages > 1);
    }

    [Fact]
    public async Task Entradas_com_o_mesmo_instante_na_fronteira_ficam_na_mesma_pagina()
    {
        using var w = await Lived();
        // A ação executada, a aprovação e o uso de modelo do pedido de engenharia têm o mesmo instante ou quase: um corte no meio
        // de um empate não pode separar entradas do mesmo instante.
        var service = w.Service();
        var all = service.Read(w.H.ProjectId, [], 200, null).Entries;
        var tied = all.GroupBy(entry => entry.At).First(group => group.Count() > 1);
        var firstIndex = all.ToList().FindIndex(entry => entry.At == tied.Key);

        var page = service.Read(w.H.ProjectId, [], firstIndex + 1, null);

        Assert.Equal(firstIndex + tied.Count(), page.Entries.Count);
    }

    [Fact]
    public async Task Projeto_nao_autorizado_e_recusado_e_o_dado_de_outro_projeto_nao_entra()
    {
        using var w = await Lived();
        w.H.GitHub.Issues.Clear();
        w.H.GitHub.Issues.Add(FakeGitHub.Issue(9, "2026-11-04T10:00:00Z"));
        var other = await w.H.Hub.Projects.LinkAsync(w.H.OtherProjectId, w.Connection.Id, FakeGitHub.Repository, [DataKinds.Issues], CancellationToken.None);
        w.Journal("ex-0000000000ff", RunKinds.Apply, RunStates.Completed, new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero),
            journal => journal.Ops.Add(new JournalOp { Path = "x.md", Op = OpKinds.Create, Status = OpStatus.Applied }), w.H.OtherProjectId);
        var service = w.Service();

        var error = Assert.Throws<EngineException>(() => service.Read("p-desconhecido01", [], null, null));
        var mine = service.Read(w.H.ProjectId, [], 200, null);
        var theirs = service.Read(w.H.OtherProjectId, [], 200, null);

        Assert.Equal("PROJECT_NOT_AUTHORIZED", error.Code);
        Assert.DoesNotContain(mine.Entries, entry => entry.RunId == "ex-0000000000ff");
        Assert.Contains(theirs.Entries, entry => entry.RunId == "ex-0000000000ff");
        Assert.NotNull(other);
    }

    private sealed class BrokenReader : IAuditReader
    {
        public IReadOnlyList<AuditRecord> ReadAll() => throw new IOException("disco com defeito: C:\\segredo\\caminho");
    }

    [Fact]
    public async Task Se_o_arquivo_de_auditoria_falha_o_diario_segue_e_o_motivo_nao_vaza()
    {
        using var w = await Lived();

        var trail = w.Service(new BrokenReader()).Read(w.H.ProjectId, [], 200, null);

        Assert.False(trail.Sources.Single(source => source.Source == "audit").Available);
        Assert.True(trail.Sources.Single(source => source.Source == "journal").Available);
        Assert.NotEmpty(trail.Entries);
        Assert.DoesNotContain("segredo", JsonSerializer.Serialize(trail, IntegrationJson.Options));
    }

    [Fact]
    public async Task Sem_leitor_do_arquivo_a_fonte_nao_existe_e_o_diario_continua()
    {
        using var w = await Lived();

        var trail = w.Service(withReader: false).Read(w.H.ProjectId, [], 200, null);

        Assert.Equal(["journal"], trail.Sources.Select(source => source.Source));
        Assert.All(trail.Entries, entry => Assert.Equal("journal", entry.Source));
    }

    [Fact]
    public async Task Ler_a_trilha_nao_grava_nada()
    {
        using var w = await Lived();
        var before = w.H.EverythingOnDisk();
        var linesBefore = w.H.Audit.ReadAll().Count;

        w.Service().Read(w.H.ProjectId, [], 200, null);

        Assert.Equal(before, w.H.EverythingOnDisk());
        Assert.Equal(linesBefore, w.H.Audit.ReadAll().Count);
    }

    [Fact]
    public async Task Nada_leva_credencial_titulo_de_item_responsavel_nem_travessao()
    {
        using var w = await Lived();

        var text = JsonSerializer.Serialize(w.Service().Read(w.H.ProjectId, [], 200, null), IntegrationJson.Options);

        Assert.DoesNotContain(FakeGitHub.Token, text);
        Assert.DoesNotContain("Item 1", text);
        Assert.DoesNotContain("dev-1", text);
        Assert.DoesNotContain("\u2014", text);
    }

    [Fact]
    public void A_linha_citada_e_a_linha_do_arquivo_mesmo_com_uma_linha_ilegivel_no_meio()
    {
        using var project = TempProject.Dotnet();
        var workspace = new Mariliz.Application.Workspace.WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var clock = new FakeClock();
        var log = new FileAuditLog(project.DataDir, clock);
        log.Record("integration.linked", new JsonObject { ["projectId"] = projectId, ["integrationId"] = "pint-000000000001", ["connectionId"] = "conn-000000000001", ["resource"] = "acme/app", ["dataKinds"] = new JsonArray("issues"), ["feedsKnowledge"] = false });
        File.AppendAllText(Path.Combine(project.DataDir, "audit.ndjson"), "{ linha cortada no meio\n", new UTF8Encoding(false));
        clock.UtcNow = clock.UtcNow.AddHours(1);
        log.Record("integration.unlinked", new JsonObject { ["projectId"] = projectId, ["integrationId"] = "pint-000000000001", ["connectionId"] = "conn-000000000001", ["resource"] = "acme/app" });

        var service = new ProjectAuditService(workspace, clock, new FileRunStore(project.DataDir), log);
        var trail = service.Read(projectId, [], 50, null);

        var lines = File.ReadAllLines(Path.Combine(project.DataDir, "audit.ndjson"));
        Assert.Equal(3, lines.Length);
        var unlinked = trail.Entries.Single(entry => entry.Title.StartsWith("Fonte desvinculada", StringComparison.Ordinal));
        Assert.Equal(3, unlinked.AuditLine);
        Assert.Contains("integration.unlinked", lines[unlinked.AuditLine!.Value - 1]);
        Assert.Equal(1, trail.Entries.Single(entry => entry.Title.StartsWith("Fonte vinculada", StringComparison.Ordinal)).AuditLine);
    }

    private static JsonObject Call(EngineSession session, JsonObject parameters) =>
        session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "audit.read", ["params"] = parameters }.ToJsonString()).Response;

    [Fact]
    public async Task O_metodo_responde_filtra_pagina_e_recusa_o_que_nao_entende()
    {
        using var w = await Lived();
        var session = new EngineSession("test", audit: w.Service());

        var all = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["limit"] = 5 });
        var next = all["result"]!["nextBefore"]!.GetValue<string>();
        var older = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["limit"] = 5, ["before"] = next });
        var models = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["categories"] = new JsonArray("model") });
        var badCategory = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["categories"] = new JsonArray("inventada") });
        var badBefore = Call(session, new JsonObject { ["projectId"] = w.H.ProjectId, ["before"] = "ontem" });
        var unknown = Call(session, new JsonObject { ["projectId"] = "p-desconhecido01" });

        Assert.Equal(5, all["result"]!["entries"]!.AsArray().Count);
        Assert.True(older["result"]!["entries"]!.AsArray().All(item => DateTimeOffset.Parse(item!["at"]!.GetValue<string>()) < DateTimeOffset.Parse(next)));
        Assert.Single(models["result"]!["entries"]!.AsArray());
        Assert.Equal("AUDIT_CATEGORY_UNKNOWN", badCategory["error"]!["code"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, badBefore["error"]!["code"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", unknown["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fixture_de_contrato_da_trilha_de_auditoria()
    {
        using var w = await Lived();
        var trail = w.Service().Read(w.H.ProjectId, [], 200, null);

        var text = JsonSerializer.Serialize(trail, IntegrationJson.Options).Replace(w.H.ProjectId, "p-000000000001");
        var pretty = JsonNode.Parse(text)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "audit", "audit.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "audit: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
        Assert.DoesNotMatch(new Regex("conn-[0-9a-f]{12}|pint-[0-9a-f]{12}"), pretty);
    }
}
