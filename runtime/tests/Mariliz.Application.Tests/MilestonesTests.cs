using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// O ritmo é o do tipo de dado inteiro, constante de 2 entregas por semana nas contas abaixo, o que torna a previsão exata e conferível à mão.
public class MilestonesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 12);

    private static WorkItemMilestone[] Marco(string name, DateOnly? due = null) => [new(name, due)];

    private static WorkItem Open(string id, WorkItemMilestone[]? milestones, int createdDaysAgo = 5) =>
        new(DataKinds.Issues, id, "Item " + id, "Issue", "open", StatusCategories.Todo, [], [], Now.AddDays(-createdDaysAgo), Now, null, null, null,
            "https://example.test/" + id, [], null, milestones);

    private static WorkItem Done(string id, WorkItemMilestone[]? milestones, DateTimeOffset closed, string status = "completed") =>
        new(DataKinds.Issues, id, "Item " + id, "Issue", status, StatusCategories.Done, [], [], Now.AddDays(-100), closed, closed, null, null,
            "https://example.test/" + id, [], null, milestones);

    /// <summary>24 entregas, 2 por semana nas últimas 12 semanas; as <paramref name="inMilestone"/> primeiras pertencem ao marco dado.</summary>
    private static List<WorkItem> Pace(string? milestone = null, int inMilestone = 0)
    {
        var items = new List<WorkItem>();
        for (var n = 0; n < 24; n++)
        {
            var closed = Now.AddDays(-(7 * (n / 2) + 3));
            items.Add(Done("h" + n, milestone is not null && n < inMilestone ? Marco(milestone) : [], closed));
        }

        return items;
    }

    private static List<MilestoneProgress> Compute(IEnumerable<WorkItem> items) =>
        MilestoneCalculator.Compute(DataKinds.Issues, items.ToList(), Now, sourcesComplete: true).Milestones;

    [Fact]
    public void Conta_itens_entregues_abertos_e_concluidos_sem_entrega_de_cada_marco()
    {
        var items = Pace("v1.0", inMilestone: 4)
            .Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0"))))
            .Append(Done("descartado", Marco("v1.0"), Now.AddDays(-2), status: "not_planned"));

        var marco = Assert.Single(Compute(items));

        Assert.Equal(("v1.0", 11, 4, 1, 6), (marco.Name, marco.Items, marco.Delivered, marco.ClosedWithoutDelivery, marco.Open));
    }

    [Fact]
    public void O_marco_agrupa_pelo_nome_sem_diferenciar_maiusculas_e_um_item_com_duas_versoes_conta_nas_duas()
    {
        var items = new[]
        {
            Open("a", Marco("V1.0")),
            Open("b", Marco("v1.0")),
            Open("c", [new WorkItemMilestone("v1.0", null), new WorkItemMilestone("v2.0", null)])
        };

        var marcos = Compute(items).OrderBy(m => m.Name).ToList();

        Assert.Equal(2, marcos.Count);
        Assert.Equal(3, marcos.Single(m => m.Name.Equals("v1.0", StringComparison.OrdinalIgnoreCase)).Items);
        Assert.Equal(1, marcos.Single(m => m.Name == "v2.0").Items);
    }

    [Fact]
    public void Item_sem_marco_ou_de_dado_sincronizado_antes_dos_marcos_conta_como_sem_marco()
    {
        var items = new[] { Open("a", Marco("v1.0")), Open("b", []), Open("c", null), Done("d", [], Now.AddDays(-1)) };

        var (marcos, semMarco) = MilestoneCalculator.Compute(DataKinds.Issues, items, Now, true);

        Assert.Single(marcos);
        Assert.Equal((3, 2), (semMarco.Items, semMarco.Open));
    }

    [Fact]
    public void O_prazo_do_marco_e_o_mais_cedo_entre_os_itens_que_o_informam()
    {
        var items = new[]
        {
            Open("a", [new WorkItemMilestone("v1.0", null)]),
            Open("b", [new WorkItemMilestone("v1.0", new DateOnly(2026, 12, 20))]),
            Open("c", [new WorkItemMilestone("v1.0", new DateOnly(2026, 12, 10))])
        };

        Assert.Equal(new DateOnly(2026, 12, 10), Assert.Single(Compute(items)).DueDate);
    }

    [Fact]
    public void A_previsao_do_marco_usa_o_ritmo_do_tipo_todo_e_so_o_que_esta_aberto_nele_conferido_a_mao()
    {
        // 6 itens abertos no marco, 2 entregas por semana no tipo todo: toda simulação termina em 3 semanas.
        var items = Pace().Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0")))).Concat(Enumerable.Range(1, 40).Select(n => Open("fora" + n, [])));

        var forecast = Assert.Single(Compute(items)).Forecast;

        Assert.Equal(6, forecast.Remaining);
        Assert.All(forecast.Scenarios, scenario => Assert.Equal((3, new DateOnly(2026, 12, 3)), (scenario.Weeks, scenario.By)));
        Assert.Null(forecast.Unavailable);
    }

    [Theory]
    [InlineData(30, MilestoneOutlook.OnTrack)]   // prazo em 12/12: 3 semanas (03/12) cabe, até o conservador
    [InlineData(21, MilestoneOutlook.OnTrack)]   // prazo em 03/12: igual à previsão cabe (<=)
    [InlineData(20, MilestoneOutlook.Behind)]    // prazo em 02/12: nem o provável cabe
    public void A_situacao_compara_o_prazo_com_a_previsao(int dueInDays, string expected)
    {
        var due = Today.AddDays(dueInDays);
        var items = Pace().Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0", due))));

        var marco = Assert.Single(Compute(items));

        Assert.Equal(expected, marco.Outlook);
    }

    [Fact]
    public void Marco_com_tudo_concluido_esta_concluido_mesmo_com_o_prazo_passado()
    {
        var items = new[] { Done("a", Marco("v1.0", Today.AddDays(-10)), Now.AddDays(-12)) };

        Assert.Equal(MilestoneOutlook.Done, Assert.Single(Compute(items)).Outlook);
    }

    [Fact]
    public void Prazo_passado_com_item_aberto_e_vencido_haja_ou_nao_previsao()
    {
        var items = Pace().Concat([Open("a", Marco("v1.0", Today.AddDays(-1))), Open("b", Marco("v1.0", Today.AddDays(-1)))]);

        var marco = Assert.Single(Compute(items));

        Assert.Equal(MilestoneOutlook.Overdue, marco.Outlook);
        Assert.Contains("ainda há 2 itens abertos", marco.OutlookReason);
    }

    [Fact]
    public void Sem_prazo_no_provedor_a_situacao_diz_isso_e_a_previsao_continua()
    {
        var items = Pace().Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0"))));

        var marco = Assert.Single(Compute(items));

        Assert.Equal(MilestoneOutlook.NoDueDate, marco.Outlook);
        Assert.Equal(3, marco.Forecast.Scenarios.Count);
    }

    [Fact]
    public void Sem_historico_suficiente_a_situacao_e_sem_previsao_com_o_motivo_da_previsao()
    {
        var items = new[] { Open("a", Marco("v1.0", Today.AddDays(30))) };

        var marco = Assert.Single(Compute(items));

        Assert.Equal(MilestoneOutlook.NoForecast, marco.Outlook);
        Assert.Contains("semanas de histórico", marco.OutlookReason);
        Assert.Empty(marco.Forecast.Scenarios);
    }

    private static DeliveryForecast Forecast(DateOnly likely, DateOnly conservative) =>
        new("issues", 6, 12, [2], 10_000,
            [new("optimistic", "Otimista", 15, 1, likely), new("likely", "Provável", 50, 3, likely), new("conservative", "Conservador", 85, 5, conservative)],
            ForecastConfidence.High, [], [], null);

    [Fact]
    public void Em_risco_quando_o_provavel_cabe_no_prazo_e_o_conservador_nao()
    {
        var due = new DateOnly(2026, 12, 10);

        Assert.Equal(MilestoneOutlook.AtRisk, MilestoneCalculator.Outlook(due, 6, Forecast(new DateOnly(2026, 12, 5), new DateOnly(2026, 12, 20)), Today).Outlook);
        Assert.Equal(MilestoneOutlook.OnTrack, MilestoneCalculator.Outlook(due, 6, Forecast(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 10)), Today).Outlook);
        Assert.Equal(MilestoneOutlook.Behind, MilestoneCalculator.Outlook(due, 6, Forecast(new DateOnly(2026, 12, 11), new DateOnly(2026, 12, 30)), Today).Outlook);
    }

    private static async Task<(IntegrationHarness Harness, ProjectIntegration Integration)> Seed(IEnumerable<WorkItem> items)
    {
        var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        var integration = await h.LinkGitHubAsync();
        var data = new SyncedData();
        foreach (var item in items) data.Upsert(item, Now);
        data.Record(new SyncExecution("sync-0000000000aa", Now, Now, SyncStatus.Succeeded, false, 1, data.Items.Count, data.Items.Count, 0, 0, null));
        new FileSyncedDataStore(h.DataDir).Write(h.ProjectId, integration.Id, data);
        return (h, integration);
    }

    private static JsonObject Request(IntegrationHarness h, string projectId) =>
        h.Request("management.milestones", new JsonObject { ["projectId"] = projectId });

    [Fact]
    public async Task O_metodo_traz_os_marcos_com_o_que_esta_aberto_primeiro_pelo_prazo()
    {
        var items = Pace().Concat(
        [
            Open("1", Marco("tarde", new DateOnly(2026, 12, 30))),
            Open("2", Marco("cedo", new DateOnly(2026, 12, 5))),
            Open("3", Marco("sem prazo")),
            Done("4", Marco("pronto", new DateOnly(2026, 11, 1)), Now.AddDays(-3))
        ]);
        var (h, _) = await Seed(items);
        using var _ = h;

        var result = Request(h, h.ProjectId)["result"]!;

        Assert.Equal(["cedo", "tarde", "sem prazo", "pronto"], result["milestones"]!.AsArray().Select(m => m!["name"]!.GetValue<string>()).ToArray());
        Assert.True(result["complete"]!.GetValue<bool>());
        Assert.NotEmpty(result["assumptions"]!.AsArray());
        Assert.NotEmpty(result["limitations"]!.AsArray());
    }

    [Fact]
    public async Task A_lista_para_em_vinte_marcos_e_diz_quantos_ficaram_de_fora()
    {
        var items = Enumerable.Range(1, 23).Select(n => Open("o" + n, Marco("marco-" + n.ToString("00"), Today.AddDays(30 + n))));
        var (h, _) = await Seed(items);
        using var _ = h;

        var result = Request(h, h.ProjectId)["result"]!;

        Assert.Equal(MilestoneCalculator.MaxMilestones, result["milestones"]!.AsArray().Count);
        Assert.Equal(3, result["omitted"]!.GetValue<int>());
    }

    [Fact]
    public async Task Projeto_nao_autorizado_e_recusado_e_um_projeto_nao_ve_os_marcos_do_outro()
    {
        var (h, _) = await Seed([Open("1", Marco("v1.0"))]);
        using var _ = h;

        var denied = Request(h, "p-desconhecido01");
        var other = Request(h, h.OtherProjectId)["result"]!;

        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
        Assert.Empty(other["milestones"]!.AsArray());
    }

    [Fact]
    public async Task O_assistente_recebe_um_fato_por_marco_com_prazo_previsao_e_situacao()
    {
        var items = Pace().Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0", Today.AddDays(30)))));
        var (h, _) = await Seed(items);
        using var _ = h;
        var management = new ManagementService(h.Workspace, h.Hub, h.Clock);

        var facts = Mariliz.Application.Management.Assistant.EvidenceBuilder.Build(h.ProjectId, "Loja", Now, h.Hub.Projects.List(h.ProjectId),
            management.Metrics(h.ProjectId, null), management.Forecast(h.ProjectId), management.Risks(h.ProjectId), management.Milestones(h.ProjectId)).Facts;

        var fact = Assert.Single(facts, f => f.Id == "milestone.1");
        Assert.Contains("Marco \"v1.0\" (Issues): 0 entregues de 6 itens, 6 abertos", fact.Text);
        Assert.Contains("prazo 2026-12-12", fact.Text);
        Assert.Contains("situação: no prazo", fact.Text);
    }

    [Fact]
    public async Task Fixture_de_contrato_dos_marcos()
    {
        var items = Pace("v1.0", 4).Concat(Enumerable.Range(1, 6).Select(n => Open("o" + n, Marco("v1.0", new DateOnly(2026, 12, 12)))))
            .Concat([Open("x", Marco("v2.0")), Open("sem", [])]);
        var (h, _) = await Seed(items);
        using var _ = h;

        var text = Request(h, h.ProjectId)["result"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(h.ProjectId, "p-000000000001").ReplaceLineEndings("\n");

        var path = ContractFiles.Path_("fixtures", "management", "milestones.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "milestones: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }

    [Fact]
    public async Task O_github_traz_o_milestone_com_a_data_de_entrega_em_utc()
    {
        using var h = new IntegrationHarness();
        var issue = FakeGitHub.Issue(1, "2026-11-08T10:00:00Z");
        issue["milestone"] = new JsonObject { ["title"] = "v1.0", ["due_on"] = "2026-12-15T08:00:00Z", ["state"] = "open" };
        h.GitHub.Issues.Add(issue);
        h.GitHub.Issues.Add(FakeGitHub.Issue(2, "2026-11-09T10:00:00Z"));
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        await h.SyncAsync(link);

        var items = h.Hub.Projects.Data(link).Items.Values.Select(stored => stored.Item).OrderBy(item => item.ExternalId).ToList();

        Assert.Equal([new WorkItemMilestone("v1.0", new DateOnly(2026, 12, 15))], items[0].Milestones);
        Assert.Empty(items[1].Milestones!);
    }

    [Fact]
    public async Task O_github_sem_data_de_entrega_traz_o_milestone_sem_prazo()
    {
        using var h = new IntegrationHarness();
        var issue = FakeGitHub.Issue(1, "2026-11-08T10:00:00Z");
        issue["milestone"] = new JsonObject { ["title"] = "v1.0", ["due_on"] = null };
        h.GitHub.Issues.Add(issue);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        await h.SyncAsync(link);

        var item = h.Hub.Projects.Data(link).Items.Values.Single().Item;

        Assert.Equal([new WorkItemMilestone("v1.0", null)], item.Milestones);
    }

    private static async Task<WorkItem> SyncedJira(IntegrationHarness h, JsonNode? fixVersions)
    {
        var issue = FakeJira.Issue("APP-1", "2026-11-08T15:30:00.000-0300", "indeterminate", "Em andamento");
        if (fixVersions is not null) issue["fields"]!["fixVersions"] = fixVersions;
        h.Jira.Issues.Add(issue);
        var jira = await h.Hub.Projects.LinkAsync(h.ProjectId, (await h.ConnectJiraAsync()).Id, "APP", [DataKinds.Issues], CancellationToken.None);
        await h.SyncAsync(jira);
        return h.Hub.Projects.Data(jira).Items.Values.Single().Item;
    }

    [Fact]
    public async Task O_jira_traz_as_versoes_de_correcao_com_a_data_de_lancamento()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJira(h, new JsonArray(
            new JsonObject { ["name"] = "1.0", ["releaseDate"] = "2026-12-15", ["released"] = false },
            new JsonObject { ["name"] = "1.1" }));

        Assert.Equal([new WorkItemMilestone("1.0", new DateOnly(2026, 12, 15)), new WorkItemMilestone("1.1", null)], item.Milestones);
        Assert.Contains(h.Transport.Requests, request => request.Url.Query.Contains("fixVersions"));
    }

    [Fact]
    public async Task O_jira_sem_versao_traz_lista_vazia_e_sem_o_campo_traz_nulo()
    {
        using var empty = new IntegrationHarness();
        using var absent = new IntegrationHarness();

        Assert.Empty((await SyncedJira(empty, new JsonArray())).Milestones!);
        Assert.Null((await SyncedJira(absent, null)).Milestones);
    }
}
