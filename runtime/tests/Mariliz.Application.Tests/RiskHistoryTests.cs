using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// Histórico de cada risco: o que as regras achavam do projeto em cada sincronização completa, e a trajetória de cada risco
/// (apareceu, subiu, mudou de itens, foi resolvido). As trajetórias abaixo foram conferidas à mão.
/// </summary>
public class RiskHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int day) => T0.AddDays(day);

    private static RiskSnapshotEntry Risk(string id, string severity, params string[] items) => new(id, "Título " + id, severity, items);

    private static RiskSnapshot Snapshot(int day, params RiskSnapshotEntry[] risks) => new(At(day), At(day), risks);

    // ------------------------------------------------------------------ Trajetórias

    [Fact]
    public void A_trajetoria_conta_o_que_mudou_em_cada_registro_conferido_a_mao()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(10),
        [
            Snapshot(0, Risk("A", "medium", "i1", "i2")),                           // A já estava lá no primeiro registro
            Snapshot(1, Risk("A", "high", "i1", "i2", "i3"), Risk("B", "low", "j1")), // A sobe e ganha i3; B aparece
            Snapshot(2, Risk("A", "high", "i1", "i3"), Risk("B", "low", "j1")),       // A perde i2
            Snapshot(3, Risk("B", "low", "j1")),                                      // A some
            Snapshot(4, Risk("A", "low", "i9"))                                       // A volta mais leve; B some
        ]);

        var a = history.Trails.Single(trail => trail.Id == "A");
        var b = history.Trails.Single(trail => trail.Id == "B");

        Assert.Equal(
            [RiskChangeKinds.Observed, RiskChangeKinds.Raised, RiskChangeKinds.ItemsChanged, RiskChangeKinds.Resolved, RiskChangeKinds.Appeared],
            a.Changes.Select(change => change.Kind));
        Assert.Equal(["i3"], a.Changes[1].ItemsAdded);
        Assert.Equal(["i2"], a.Changes[2].ItemsRemoved);
        Assert.Equal(("medium", "high"), (a.Changes[1].From, a.Changes[1].To));
        Assert.True(a.Active);
        Assert.Equal("low", a.CurrentSeverity);
        Assert.Equal(At(0), a.FirstSeenAt);
        Assert.Null(a.ResolvedAt);

        Assert.Equal([RiskChangeKinds.Appeared, RiskChangeKinds.Resolved], b.Changes.Select(change => change.Kind));
        Assert.False(b.Active);
        Assert.Null(b.CurrentSeverity);
        Assert.Equal((At(1), (DateTimeOffset?)At(4)), (b.FirstSeenAt, b.ResolvedAt));
    }

    [Fact]
    public void Um_risco_que_ja_estava_no_primeiro_registro_e_observado_e_nao_aparecido()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(5), [Snapshot(0, Risk("A", "high"))]);

        Assert.Equal(RiskChangeKinds.Observed, history.Trails.Single().Changes.Single().Kind);
    }

    [Fact]
    public void Gravidade_que_cai_e_abaixada()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(5), [Snapshot(0, Risk("A", "high")), Snapshot(1, Risk("A", "medium"))]);

        Assert.Equal(RiskChangeKinds.Lowered, history.Trails.Single().Changes[1].Kind);
    }

    [Fact]
    public void Gravidade_desconhecida_de_um_registro_antigo_ou_editado_nao_derruba_o_historico()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(9),
        [
            Snapshot(0, Risk("A", "high"), Risk("B", "low")),
            Snapshot(1, Risk("A", "critical"), Risk("B", "low"))   // "critical" não existe na escala do motor
        ]);

        var a = history.Trails.Single(trail => trail.Id == "A");
        Assert.Equal(["B", "A"], history.Trails.Select(trail => trail.Id));   // sem posição na escala, A fica depois de B ("low")
        Assert.Equal("critical", a.CurrentSeverity);                          // o texto do registro é mostrado como veio
        Assert.Equal(("high", "critical"), (a.Changes[1].From, a.Changes[1].To));
    }

    [Fact]
    public void Ativos_vem_primeiro_do_mais_grave_ao_mais_leve_e_resolvidos_depois_do_mais_recente()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(9),
        [
            Snapshot(0, Risk("velho", "high"), Risk("recente", "high"), Risk("leve", "low"), Risk("medio", "medium")),
            Snapshot(1, Risk("recente", "high"), Risk("leve", "low"), Risk("medio", "medium")),   // velho resolvido no dia 1
            Snapshot(2, Risk("leve", "low"), Risk("medio", "medium"))                              // recente resolvido no dia 2
        ]);

        Assert.Equal(["medio", "leve", "recente", "velho"], history.Trails.Select(trail => trail.Id));
    }

    [Fact]
    public void Sem_registros_nao_ha_historico_e_a_base_diz_o_que_isso_significa()
    {
        var history = RiskHistoryBuilder.Build("p-000000000001", At(5), []);

        Assert.Empty(history.Trails);
        Assert.Null(history.Since);
        Assert.Equal(0, history.Snapshots);
        Assert.Contains("O que aconteceu antes do primeiro registro", history.Basis);
    }

    [Fact]
    public void As_trajetorias_passam_do_limite_e_dizem_quantas_ficaram_de_fora()
    {
        var risks = Enumerable.Range(1, 35).Select(n => Risk("r" + n.ToString("00"), "low")).ToArray();

        var history = RiskHistoryBuilder.Build("p-000000000001", At(1), [Snapshot(0, risks)]);

        Assert.Equal((RiskHistoryBuilder.MaxTrails, 5), (history.Trails.Count, history.Omitted));
    }

    // ------------------------------------------------------------------ Registro

    [Fact]
    public void Estado_igual_ao_ultimo_so_confirma_e_estado_diferente_vira_registro_novo()
    {
        var first = RiskHistoryBuilder.Append([], [Risk("A", "high", "i1")], At(0));
        var same = RiskHistoryBuilder.Append(first, [Risk("A", "high", "i1")], At(3));
        var changed = RiskHistoryBuilder.Append(same, [Risk("A", "high", "i1", "i2")], At(5));

        Assert.Single(same);
        Assert.Equal((At(0), At(3)), (same[0].At, same[0].CheckedAt));
        Assert.Equal(2, changed.Count);
        Assert.Equal(At(5), changed[1].At);
    }

    [Fact]
    public void Os_itens_do_mesmo_risco_em_outra_ordem_nao_contam_como_mudanca()
    {
        var a = RiskHistoryBuilder.Entries([new ProjectRisk("x", "T", RiskSeverity.Low, "r", [], [new RiskItem("issues", "2", "t"), new RiskItem("issues", "1", "t")], null, "r", "rule")]);
        var b = RiskHistoryBuilder.Entries([new ProjectRisk("x", "T", RiskSeverity.Low, "r", [], [new RiskItem("issues", "1", "t"), new RiskItem("issues", "2", "t")], null, "r", "rule")]);

        Assert.True(RiskHistoryBuilder.Same(a, b));
    }

    [Fact]
    public void O_registro_guarda_so_identificadores_nunca_o_titulo_do_item()
    {
        var entries = RiskHistoryBuilder.Entries([new ProjectRisk("blocked", "Itens bloqueados", RiskSeverity.Low, "r", [], [new RiskItem("issues", "41", "Pagamento por Pix")], null, "r", "rule")]);

        Assert.Equal(["41"], entries[0].Items);
        Assert.DoesNotContain("Pix", System.Text.Json.JsonSerializer.Serialize(entries));
    }

    [Fact]
    public void Guarda_so_os_duzentos_registros_mais_recentes()
    {
        var list = new List<RiskSnapshot>();
        for (var n = 0; n < RiskHistoryBuilder.MaxSnapshots + 5; n++)
            list = RiskHistoryBuilder.Append(list, [Risk("A", "low", "i" + n)], At(n));

        Assert.Equal(RiskHistoryBuilder.MaxSnapshots, list.Count);
        Assert.Equal(At(5), list[0].At);
    }

    // ------------------------------------------------------------------ Serviço e sincronização

    private sealed class MemoryStore : IRiskHistoryStore
    {
        public Dictionary<string, List<RiskSnapshot>> Data { get; } = [];
        public IReadOnlyList<RiskSnapshot> Load(string projectId) => Data.GetValueOrDefault(projectId) ?? [];
        public void Save(string projectId, IReadOnlyList<RiskSnapshot> snapshots) => Data[projectId] = [.. snapshots];
    }

    private static WorkItem Item(string id, string[]? labels = null, DateOnly? due = null) =>
        new(DataKinds.Issues, id, "Item " + id, "Issue", "open", StatusCategories.InProgress, [], labels ?? [], T0.AddDays(-3), T0.AddDays(-1),
            null, due, null, "https://example.test/" + id, []);

    private static async Task<(IntegrationHarness H, ManagementService Service, MemoryStore Store, ProjectIntegration Integration)> Setup(
        IEnumerable<WorkItem> items, SyncStatus last = SyncStatus.Succeeded)
    {
        var h = new IntegrationHarness();
        h.Clock.UtcNow = T0;
        var store = new MemoryStore();
        var integration = await h.LinkGitHubAsync();
        Write(h, integration, items, last);
        return (h, new ManagementService(h.Workspace, h.Hub, h.Clock, store), store, integration);
    }

    private static void Write(IntegrationHarness h, ProjectIntegration integration, IEnumerable<WorkItem> items, SyncStatus last = SyncStatus.Succeeded)
    {
        var data = new SyncedData();
        foreach (var item in items) data.Upsert(item, T0);
        data.Record(new SyncExecution("sync-0000000000aa", T0, T0, last, false, 1, data.Items.Count, data.Items.Count, 0, 0,
            last == SyncStatus.Failed ? new IntegrationError("INTEGRATION_UNAVAILABLE", "x", T0) : null));
        new FileSyncedDataStore(h.DataDir).Write(h.ProjectId, integration.Id, data);
    }

    [Fact]
    public async Task Registra_o_estado_dos_riscos_e_confirma_sem_duplicar_quando_nada_mudou()
    {
        var (h, service, store, _) = await Setup([Item("1", ["blocked"])]);
        using var _ = h;

        Assert.True(service.CaptureRisks(h.ProjectId));
        h.Clock.UtcNow = T0.AddDays(2);
        Assert.False(service.CaptureRisks(h.ProjectId));

        var snapshot = Assert.Single(store.Data[h.ProjectId]);
        Assert.Equal((T0, T0.AddDays(2)), (snapshot.At, snapshot.CheckedAt));
        Assert.Equal("blocked", Assert.Single(snapshot.Risks).Id);
    }

    [Fact]
    public async Task Uma_mudanca_nos_dados_vira_um_registro_novo_e_aparece_na_trajetoria()
    {
        var (h, service, _, integration) = await Setup([Item("1", ["blocked"])]);
        using var _ = h;
        service.CaptureRisks(h.ProjectId);

        h.Clock.UtcNow = T0.AddDays(7);
        Write(h, integration, [Item("1", ["blocked"]), Item("2", ["blocked"])]);
        Assert.True(service.CaptureRisks(h.ProjectId));

        var trail = Assert.Single(service.RiskHistory(h.ProjectId).Trails);
        Assert.Equal([RiskChangeKinds.Observed, RiskChangeKinds.Raised], trail.Changes.Select(change => change.Kind));
        Assert.Equal(["2"], trail.Changes[1].ItemsAdded);
        Assert.Equal("medium", trail.CurrentSeverity);
    }

    [Fact]
    public async Task Um_risco_que_some_dos_dados_aparece_como_resolvido()
    {
        var (h, service, _, integration) = await Setup([Item("1", ["blocked"])]);
        using var _ = h;
        service.CaptureRisks(h.ProjectId);

        h.Clock.UtcNow = T0.AddDays(4);
        Write(h, integration, [Item("1")]);
        service.CaptureRisks(h.ProjectId);

        var trail = Assert.Single(service.RiskHistory(h.ProjectId).Trails);
        Assert.False(trail.Active);
        Assert.Equal(T0.AddDays(4), trail.ResolvedAt);
    }

    [Fact]
    public async Task Fonte_incompleta_ou_sem_dados_nao_registra_para_nao_inventar_resolucao()
    {
        var (failed, failedService, failedStore, _) = await Setup([Item("1", ["blocked"])], SyncStatus.Failed);
        var (empty, emptyService, emptyStore, _) = await Setup([]);
        using var _1 = failed;
        using var _2 = empty;

        Assert.False(failedService.CaptureRisks(failed.ProjectId));
        Assert.False(emptyService.CaptureRisks(empty.ProjectId));

        Assert.Empty(failedStore.Data);
        Assert.Empty(emptyStore.Data);
    }

    [Fact]
    public async Task Sem_loja_de_historico_nada_e_gravado_e_o_historico_vem_vazio()
    {
        var h = new IntegrationHarness();
        using var _ = h;
        var service = new ManagementService(h.Workspace, h.Hub, h.Clock);

        Assert.False(service.CaptureRisks(h.ProjectId));
        Assert.Empty(service.RiskHistory(h.ProjectId).Trails);
    }

    [Fact]
    public async Task O_historico_de_projeto_nao_autorizado_e_recusado_e_um_projeto_nao_ve_o_do_outro()
    {
        var (h, service, _, _) = await Setup([Item("1", ["blocked"])]);
        using var _ = h;
        service.CaptureRisks(h.ProjectId);

        Assert.Equal("PROJECT_NOT_AUTHORIZED", Assert.Throws<EngineException>(() => service.RiskHistory("p-desconhecido01")).Code);
        Assert.Empty(service.RiskHistory(h.OtherProjectId).Trails);
    }

    [Fact]
    public async Task O_fim_da_sincronizacao_dispara_o_registro_pelo_aviso_de_fim()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = T0;
        var store = new MemoryStore();
        var service = new ManagementService(h.Workspace, h.Hub, h.Clock, store);
        h.Hub.Sync.Finished += (integration, run) =>
        {
            if (run.Status == SyncStatus.Succeeded) service.CaptureRisks(integration.ProjectId);
        };
        var issue = FakeGitHub.Issue(1, "2026-10-30T10:00:00Z");
        issue["labels"] = new JsonArray(new JsonObject { ["name"] = "blocked" });
        h.GitHub.Issues.Add(issue);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        await h.SyncAsync(link);

        var trail = Assert.Single(service.RiskHistory(h.ProjectId).Trails);
        Assert.Equal(("blocked", RiskChangeKinds.Observed), (trail.Id, trail.Changes.Single().Kind));
    }

    [Fact]
    public async Task Um_ouvinte_que_falha_nao_desfaz_a_sincronizacao_e_o_tipo_do_erro_fica_na_trilha()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-10-30T10:00:00Z"));
        h.Hub.Sync.Finished += (_, _) => throw new InvalidOperationException("defeito do ouvinte");
        var link = await h.LinkGitHubAsync(DataKinds.Issues);

        var run = await h.SyncAsync(link);

        Assert.Equal(SyncStatus.Succeeded, run.Status);
        Assert.Single(h.Hub.Projects.Data(link).Items);
        Assert.False(h.Hub.Activity.IsRunning(link.Id));
        var audit = Assert.Single(h.Audit.Entries, entry => entry.Action == "integration.sync_listener_failed");
        Assert.Contains("InvalidOperationException", audit.Detail);
        Assert.DoesNotContain("defeito do ouvinte", audit.Detail);
        Assert.Equal("sync.finished", h.Events.Last().Type);
    }

    [Fact]
    public async Task Falha_ao_registrar_o_fim_da_sincronizacao_nao_deixa_a_interface_sem_o_aviso_de_fim()
    {
        using var h = new IntegrationHarness();
        h.GitHub.Issues.Add(FakeGitHub.Issue(1, "2026-10-30T10:00:00Z"));
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        h.Audit.FailOnAction = "integration.synced";

        // O desfecho já foi gravado: a falha no registro de trilha não pode desfazê-lo nem silenciar o aviso.
        var run = await h.SyncAsync(link);

        Assert.Equal(SyncStatus.Succeeded, run.Status);
        Assert.False(h.Hub.Activity.IsRunning(link.Id));
        Assert.Equal("sync.finished", h.Events.Last().Type);
        var failed = Assert.Single(h.Audit.Entries, entry => entry.Action == "integration.sync_bookkeeping_failed");
        Assert.Contains("IOException", failed.Detail);
    }

    private sealed class FinishesDuringReadStore(ISyncedDataStore inner, Action afterRead) : ISyncedDataStore
    {
        private bool _fired;

        public SyncedData Read(string projectId, string integrationId)
        {
            var snapshot = inner.Read(projectId, integrationId);
            if (!_fired)
            {
                _fired = true;
                afterRead();
            }
            return snapshot;
        }

        public void Write(string projectId, string integrationId, SyncedData data) => inner.Write(projectId, integrationId, data);
        public void Delete(string projectId, string integrationId) => inner.Delete(projectId, integrationId);
    }

    [Fact]
    public async Task Uma_sincronizacao_que_termina_enquanto_o_historico_e_lido_nao_vira_interrompida()
    {
        using var h = new IntegrationHarness();
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        var disk = new FileSyncedDataStore(h.DataDir);
        var running = new SyncExecution("sync-0000000000aa", h.Clock.UtcNow, null, SyncStatus.Running, false, 0, 0, 0, 0, 0, null);
        var seed = new SyncedData();
        seed.Record(running);
        disk.Write(link.ProjectId, link.Id, seed);
        Assert.True(h.Hub.Activity.TryBegin(link.Id, running.Id));

        // Entre ler o arquivo e perguntar quem está rodando, a sincronização termina: grava o desfecho e libera a integração.
        var racing = new FinishesDuringReadStore(disk, () =>
        {
            var done = disk.Read(link.ProjectId, link.Id);
            done.Record(running with { Status = SyncStatus.Succeeded, FinishedAt = h.Clock.UtcNow, Fetched = 1, Created = 1, Pages = 1 });
            disk.Write(link.ProjectId, link.Id, done);
            h.Hub.Activity.End(link.Id);
        });
        var sync = new SyncService(h.Hub.Projects, h.Hub.Connections, h.Hub.Connectors, racing, h.Hub.Activity, h.Clock, h.Audit);

        sync.History(link.ProjectId, link.Id);

        var final = Assert.Single(disk.Read(link.ProjectId, link.Id).History);
        Assert.Equal(SyncStatus.Succeeded, final.Status);
        Assert.Null(final.Error);
    }

    // ------------------------------------------------------------------ Arquivo

    [Fact]
    public void O_arquivo_guarda_e_devolve_os_registros_e_um_arquivo_ilegivel_vale_como_sem_historico()
    {
        using var project = TempProject.Dotnet();
        var store = new FileRiskHistoryStore(project.DataDir);
        var snapshots = new[] { Snapshot(0, Risk("A", "high", "i1")) };

        store.Save("p-000000000001", snapshots);
        var loaded = store.Load("p-000000000001");

        Assert.Equal("A", loaded.Single().Risks.Single().Id);
        File.WriteAllText(Path.Combine(project.DataDir, "projects", "p-000000000001", "risk-history.json"), "{ isto não é json");
        Assert.Empty(store.Load("p-000000000001"));
    }

    [Theory]
    [InlineData("../fora")]
    [InlineData("p-000000000001/../x")]
    [InlineData("")]
    public void Um_identificador_forjado_nao_aponta_para_fora_da_pasta_de_dados(string projectId)
    {
        using var project = TempProject.Dotnet();
        var store = new FileRiskHistoryStore(project.DataDir);

        Assert.Throws<EngineException>(() => store.Load(projectId));
        Assert.Throws<EngineException>(() => store.Save(projectId, []));
    }

    // ------------------------------------------------------------------ Protocolo e contrato

    private static JsonObject Call(IntegrationHarness h, ManagementService service, string projectId)
    {
        var session = new EngineSession("test", workspace: h.Workspace, integrations: h.Hub, management: service);
        return session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "management.riskhistory",
            ["params"] = new JsonObject { ["projectId"] = projectId }
        }.ToJsonString()).Response;
    }

    [Fact]
    public async Task O_metodo_devolve_as_trajetorias_e_recusa_projeto_nao_autorizado()
    {
        var (h, service, _, _) = await Setup([Item("1", ["blocked"])]);
        using var _ = h;
        service.CaptureRisks(h.ProjectId);

        var ok = Call(h, service, h.ProjectId)["result"]!;
        var denied = Call(h, service, "p-desconhecido01");

        Assert.Equal("blocked", ok["trails"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(1, ok["snapshots"]!.GetValue<int>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fixture_de_contrato_do_historico_de_risco()
    {
        var (h, service, _, integration) = await Setup([Item("1", ["blocked"]), Item("2", due: new DateOnly(2026, 10, 20))]);
        using var _ = h;
        service.CaptureRisks(h.ProjectId);
        h.Clock.UtcNow = T0.AddDays(7);
        Write(h, integration, [Item("1", ["blocked"]), Item("2", ["blocked"]), Item("3", due: new DateOnly(2026, 10, 20))]);
        service.CaptureRisks(h.ProjectId);
        h.Clock.UtcNow = T0.AddDays(14);
        Write(h, integration, [Item("2", ["blocked"])]);
        service.CaptureRisks(h.ProjectId);
        h.Clock.UtcNow = T0.AddDays(15);

        var text = Call(h, service, h.ProjectId)["result"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(h.ProjectId, "p-000000000001").ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "management", "risk-history.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "risk-history: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
