using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// Datas relativas ao relógio do teste, para que cada número esperado possa ser conferido à mão.
public class ManagementTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static WorkItem Item(
        string id,
        string category,
        DateTimeOffset created,
        DateTimeOffset? closed = null,
        string kind = DataKinds.Issues,
        string? status = null,
        DateOnly? due = null,
        IReadOnlyList<WorkItemTransition>? transitions = null,
        IReadOnlyList<string>? labels = null) =>
        new(kind, id, "Item " + id, kind == DataKinds.Issues ? "Issue" : "Pull request",
            status ?? (category == StatusCategories.Done ? "completed" : "open"), category, ["dev"], labels ?? [], created,
            closed ?? created, closed, due, null, "https://example.test/" + id, transitions ?? []);

    private static async Task<ProjectIntegration> Seed(IntegrationHarness h, IEnumerable<WorkItem> items, SyncStatus last = SyncStatus.Succeeded)
    {
        var integration = await h.LinkGitHubAsync();
        var store = new FileSyncedDataStore(h.DataDir);
        var data = new SyncedData();
        foreach (var item in items) data.Upsert(item, Now);
        data.Record(new SyncExecution("sync-0000000000aa", Now, Now, last, false, 1, data.Items.Count, data.Items.Count, 0, 0,
            last == SyncStatus.Failed ? new IntegrationError("INTEGRATION_UNAVAILABLE", "x", Now) : null));
        store.Write(h.ProjectId, integration.Id, data);
        return integration;
    }

    [Fact]
    public void Throughput_conta_entregas_por_janela_de_7_dias_terminando_agora()
    {
        var items = new[]
        {
            Item("1", StatusCategories.Done, Now.AddDays(-30), Now.AddDays(-1)),
            Item("2", StatusCategories.Done, Now.AddDays(-30), Now.AddDays(-6.9)),
            Item("3", StatusCategories.Done, Now.AddDays(-30), Now.AddDays(-7.1)),
            Item("4", StatusCategories.Done, Now.AddDays(-30), Now.AddDays(-60)),
            Item("5", StatusCategories.InProgress, Now.AddDays(-2)),
            Item("6", StatusCategories.Todo, Now.AddDays(-9))
        };

        var flow = FlowCalculator.Compute(DataKinds.Issues, items, Now, weeks: 4);

        // Uma janela inclui o início e exclui o fim: -6,9 dias cai na última, -7,1 na anterior.
        Assert.Equal([0, 0, 1, 2], flow.Throughput.Weeks.Select(w => w.Delivered));
        Assert.Equal([0, 0, 1, 1], flow.Throughput.Weeks.Select(w => w.Created));
        Assert.Equal(Now, flow.Throughput.Weeks[^1].To);
        Assert.Equal(Now.AddDays(-28), flow.Throughput.Weeks[0].From);
        Assert.Equal((3, 0.8), (flow.Throughput.Total!.Value, flow.Throughput.WeeklyAverage!.Value));
        Assert.Equal(1, flow.Wip.Value);
        Assert.Equal((6, 2), (flow.Items, flow.Open));
    }

    [Fact]
    public void Descartado_duplicado_e_pull_request_fechado_sem_merge_nao_sao_entrega()
    {
        var items = new[]
        {
            Item("1", StatusCategories.Done, Now.AddDays(-10), Now.AddDays(-1), status: "not_planned"),
            Item("2", StatusCategories.Done, Now.AddDays(-10), Now.AddDays(-1), status: "duplicate"),
            Item("3", StatusCategories.Done, Now.AddDays(-10), Now.AddDays(-1), DataKinds.PullRequests, "closed"),
            Item("4", StatusCategories.Done, Now.AddDays(-10), Now.AddDays(-1), DataKinds.PullRequests, "merged"),
            Item("5", StatusCategories.Done, Now.AddDays(-10), Now.AddDays(-1), status: "completed")
        };

        Assert.Equal(["4", "5"], items.Where(DeliveryRules.IsDelivered).Select(i => i.ExternalId));
    }

    [Fact]
    public void Lead_time_e_mediana_e_percentil_85_por_posicao_e_some_com_amostra_pequena()
    {
        double[] days = [1, 2, 3, 4, 10, 20, 30];
        var items = days.Select((d, i) => Item($"{i}", StatusCategories.Done, Now.AddDays(-d - 1), Now.AddDays(-1))).ToList();

        var flow = FlowCalculator.Compute(DataKinds.Issues, items, Now, 8);
        var small = FlowCalculator.Compute(DataKinds.Issues, items.Take(4).ToList(), Now, 8);

        // Posição ⌈0,5·7⌉ = 4 → 4 dias; posição ⌈0,85·7⌉ = 6 → 20 dias.
        Assert.Equal((7, 4.0, 20.0), (flow.LeadTime.Sample, flow.LeadTime.MedianDays!.Value, flow.LeadTime.P85Days!.Value));
        Assert.Null(flow.LeadTime.Unavailable);
        Assert.Equal((4, (double?)null), (small.LeadTime.Sample, small.LeadTime.MedianDays));
        Assert.Contains("ao menos 5", small.LeadTime.Unavailable);
    }

    [Fact]
    public void Atraso_so_existe_quando_a_fonte_informa_prazo()
    {
        var github = new[] { Item("1", StatusCategories.Todo, Now.AddDays(-3)) };
        var jira = new[]
        {
            Item("A-1", StatusCategories.Todo, Now.AddDays(-3), due: new DateOnly(2026, 11, 9)),
            Item("A-2", StatusCategories.Todo, Now.AddDays(-3), due: new DateOnly(2026, 11, 10)),
            Item("A-3", StatusCategories.Done, Now.AddDays(-3), Now.AddDays(-1), due: new DateOnly(2026, 11, 1)),
            Item("A-4", StatusCategories.Todo, Now.AddDays(-3))
        };

        var none = FlowCalculator.Compute(DataKinds.Issues, github, Now, 8).Overdue;
        var some = FlowCalculator.Compute(DataKinds.Issues, jira, Now, 8).Overdue;

        Assert.Equal(((int?)null, "A fonte não informa prazo para estes itens."), (none.Value, none.Unavailable));
        Assert.Equal((1, 2), (some.Value!.Value, some.WithDueDate));
    }

    [Fact]
    public void Sem_itens_nenhum_indicador_tem_valor()
    {
        var flow = FlowCalculator.Compute(DataKinds.Issues, [], Now, 8);

        Assert.All(new[] { flow.Wip.Unavailable, flow.Throughput.Unavailable, flow.LeadTime.Unavailable, flow.CycleTime.Unavailable, flow.Blocked.Unavailable, flow.Overdue.Unavailable },
            reason => Assert.Equal(FlowCalculator.NoItems, reason));
        Assert.Null(flow.Wip.Value);
        Assert.Null(flow.Throughput.Total);
        Assert.Empty(flow.Throughput.Weeks);
    }

    [Fact]
    public async Task Indicadores_separam_os_tipos_marcam_fonte_incompleta_e_se_repetem_com_os_mesmos_dados()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        await Seed(h, [
            Item("1", StatusCategories.Done, Now.AddDays(-5), Now.AddDays(-1)),
            Item("2", StatusCategories.Done, Now.AddDays(-5), Now.AddDays(-2), DataKinds.PullRequests, "merged"),
            Item("3", StatusCategories.InProgress, Now.AddDays(-1), kind: DataKinds.PullRequests, status: "open")
        ], SyncStatus.Partial);

        var first = h.Request("management.metrics", new JsonObject { ["projectId"] = h.ProjectId, ["weeks"] = 4 })["result"]!;
        var second = h.Request("management.metrics", new JsonObject { ["projectId"] = h.ProjectId, ["weeks"] = 4 })["result"]!;

        Assert.Equal(first.ToJsonString(), second.ToJsonString());
        Assert.False(first["complete"]!.GetValue<bool>());
        Assert.Equal("partial", first["sources"]![0]!["lastSyncStatus"]!.GetValue<string>());
        Assert.Equal(["issues", "pullRequests"], first["kinds"]!.AsArray().Select(k => k!["kind"]!.GetValue<string>()));
        Assert.Equal(1, first["kinds"]![1]!["wip"]!["value"]!.GetValue<int>());
        Assert.Equal(4, first["periodWeeks"]!.GetValue<int>());
        Assert.Contains(first["notCalculated"]!.AsArray(), n => n!["id"]!.GetValue<string>() == "scopeChange");
        Assert.DoesNotContain(first["notCalculated"]!.AsArray(), n => n!["id"]!.GetValue<string>() == "cycleTime");
        Assert.DoesNotContain(first["notCalculated"]!.AsArray(), n => n!["id"]!.GetValue<string>() == "blockers");
        Assert.DoesNotContain(first["notCalculated"]!.AsArray(), n => n!["id"]!.GetValue<string>() == "forecast");
    }

    [Fact]
    public async Task Indicadores_recusam_projeto_nao_autorizado_e_periodo_fora_do_limite()
    {
        using var h = new IntegrationHarness();

        var denied = h.Request("management.metrics", new JsonObject { ["projectId"] = "p-000000000000" });
        var tooLong = h.Request("management.metrics", new JsonObject { ["projectId"] = h.ProjectId, ["weeks"] = 52 });
        var empty = h.Request("management.metrics", new JsonObject { ["projectId"] = h.ProjectId })["result"]!;

        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", tooLong["error"]!["code"]!.GetValue<string>());
        Assert.Empty(empty["kinds"]!.AsArray());
        Assert.False(empty["complete"]!.GetValue<bool>());
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Portfolio_lista_cada_projeto_so_com_os_proprios_dados_e_aponta_o_que_pede_atencao()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        await Seed(h, [
            Item("1", StatusCategories.InProgress, Now.AddDays(-3)),
            Item("2", StatusCategories.Done, Now.AddDays(-20), Now.AddDays(-2)),
            Item("3", StatusCategories.Done, Now.AddDays(-200), Now.AddDays(-100))
        ]);

        var portfolio = h.Management.Portfolio();

        Assert.Equal(2, portfolio.Projects.Count);
        var mine = portfolio.Projects.Single(p => p.ProjectId == h.ProjectId);
        var other = portfolio.Projects.Single(p => p.ProjectId == h.OtherProjectId);
        Assert.Equal(new KindSnapshot(DataKinds.Issues, 3, 1, 1, 1, null), Assert.Single(mine.Kinds));
        Assert.Empty(mine.Signals);
        Assert.Equal(Path.GetFileName(h.Project.Root), mine.DisplayName);
        // A raiz só pode aparecer como caminho de exibição: no Linux, /tmp/x é curto demais para ser abreviado. Comparada já
        // escapada como JSON, senão as barras do Windows nunca casariam e o teste não conferiria nada.
        var json = System.Text.Json.JsonSerializer.Serialize(portfolio);
        string Escaped(string text) => System.Text.Json.JsonSerializer.Serialize(text)[1..^1];
        Assert.DoesNotContain(Escaped(h.Project.Root), json.Replace($"\"DisplayPath\":\"{Escaped(mine.DisplayPath)}\"", ""));
        Assert.Empty(other.Kinds);
        Assert.Equal(0, other.Sources);
        Assert.Equal("no-sources", Assert.Single(other.Signals).Id);
    }

    [Fact]
    public async Task Portfolio_poe_primeiro_quem_pede_atencao_e_nao_conta_a_falta_de_ferramenta_como_atencao()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        await Seed(h, [Item("A-1", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1))]);
        h.Clock.UtcNow = Now.AddDays(10);

        var projects = h.Management.Portfolio().Projects;

        Assert.Equal(h.ProjectId, projects[0].ProjectId);
        Assert.Contains(projects[0].Signals, signal => signal.Id == "overdue");
        Assert.Equal(h.OtherProjectId, projects[1].ProjectId);
        Assert.Equal(["no-sources"], projects[1].Signals.Select(signal => signal.Id));
    }

    [Fact]
    public async Task Portfolio_aponta_sincronizacao_incompleta_dados_antigos_e_prazo_vencido()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        await Seed(h, [Item("A-1", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1))], SyncStatus.Failed);
        h.Clock.UtcNow = Now.AddDays(10);

        var mine = h.Management.Portfolio().Projects.Single(p => p.ProjectId == h.ProjectId);

        Assert.Equal(["sync-incomplete", "stale", "overdue"], mine.Signals.Select(s => s.Id));
        Assert.Equal("Dados lidos há 10 dias.", mine.Signals[1].Message);
        Assert.Equal("1 item aberto com prazo vencido.", mine.Signals[2].Message);
    }

    [Fact]
    public async Task Respostas_da_gestao_conferem_com_as_fixtures_do_contrato()
    {
        using var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        await Seed(h, Enumerable.Range(1, 6)
            // Cada item levou n dias em andamento (de "open" a "completed"): cycle time mediano 3, percentil 85 6.
            .Select(n => Item($"{n}", StatusCategories.Done, Now.AddDays(-3 * n - 2), Now.AddDays(-3 * n), transitions:
                [new WorkItemTransition(Now.AddDays(-3 * n - n), "new", "open"), new WorkItemTransition(Now.AddDays(-3 * n), "open", "completed")]))
            // O item 7 está em andamento e marcado como bloqueado: aparece nos indicadores e como risco.
            .Append(Item("7", StatusCategories.InProgress, Now.AddDays(-4), labels: ["blocked"]))
            .Append(Item("8", StatusCategories.Done, Now.AddDays(-9), Now.AddDays(-1), DataKinds.PullRequests, "merged")));

        var scenarios = new Dictionary<string, JsonNode>
        {
            ["portfolio"] = h.Request("management.portfolio", new JsonObject())["result"]!,
            ["metrics"] = h.Request("management.metrics", new JsonObject { ["projectId"] = h.ProjectId, ["weeks"] = 4 })["result"]!
        };

        foreach (var (name, result) in scenarios)
        {
            var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            text = text.Replace(h.ProjectId, "p-000000000001").Replace(h.OtherProjectId, "p-000000000002")
                .Replace(Path.GetFileName(h.Project.Root), "loja").Replace(Path.GetFileName(h.OtherProject.Root), "portal");
            text = System.Text.RegularExpressions.Regex.Replace(text, "pint-[0-9a-f]{12}", "pint-000000000001");
            text = System.Text.RegularExpressions.Regex.Replace(text, "\"displayPath\": \"[^\"]*\"", "\"displayPath\": \"~\"");
            text = text.ReplaceLineEndings("\n");

            var path = ContractFiles.Path_("fixtures", "management", name + ".json");
            if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text + "\n");
                continue;
            }
            Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
                $"{name}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
        }
    }
}
