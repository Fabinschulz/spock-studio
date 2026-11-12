using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

/// <summary>
/// Itens bloqueados: o rótulo "blocked" ou "bloqueado", ou o vínculo "is blocked by" do Jira com um item ainda aberto. Só conta
/// o item aberto, e o que cada fonte permitiu ler é dito. As contas abaixo foram feitas à mão.
/// </summary>
public class BlockersTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static WorkItem Item(
        string id, string category = StatusCategories.InProgress, string[]? labels = null, string[]? blockedBy = null,
        int createdDaysAgo = 10, string kind = DataKinds.Issues) =>
        new(kind, id, "Item " + id, "Story", category == StatusCategories.Done ? "Done" : "Open", category, [], labels ?? [],
            Now.AddDays(-createdDaysAgo), Now, category == StatusCategories.Done ? Now.AddDays(-1) : null, null, null,
            "https://example.test/" + id, [], blockedBy);

    private static BlockedMetric Blocked(params WorkItem[] items) =>
        FlowCalculator.Compute(DataKinds.Issues, items, Now, 8).Blocked;

    // ------------------------------------------------------------------ Cálculo

    [Fact]
    public void Conta_o_item_aberto_bloqueado_por_rotulo_por_vinculo_ou_pelos_dois_conferido_a_mao()
    {
        var metric = Blocked(
            Item("A", labels: ["Blocked"]),                                    // rótulo (sem diferenciar maiúsculas)
            Item("B", blockedBy: ["X-1"]),                                     // vínculo
            Item("C", labels: ["bloqueado"], blockedBy: ["X-2", "X-3"]),       // os dois
            Item("D", StatusCategories.Done, labels: ["blocked"]),             // concluído: não conta
            Item("E", blockedBy: []),                                          // vínculo lido, sem bloqueio
            Item("F"));                                                        // nada lido

        Assert.Equal(3, metric.Value);      // A, B, C
        Assert.Equal(2, metric.ViaLink);    // B, C
        Assert.Equal(2, metric.ViaLabel);   // A, C
        Assert.Equal(3, metric.LinksRead);  // B, C, E
        Assert.Null(metric.Unavailable);
    }

    [Theory]
    [InlineData("blocked")]
    [InlineData("BLOCKED")]
    [InlineData("bloqueado")]
    [InlineData("Bloqueada")]
    public void Os_rotulos_de_bloqueio_nao_diferenciam_maiusculas_nem_o_genero(string label)
    {
        Assert.Equal(1, Blocked(Item("A", labels: [label])).ViaLabel);
    }

    [Fact]
    public void Rotulo_parecido_nao_conta()
    {
        var metric = Blocked(Item("A", labels: ["unblocked"]), Item("B", labels: ["blocker"]), Item("C", labels: ["bug"]));

        Assert.Equal(0, metric.Value);
    }

    [Fact]
    public void Sem_vinculos_lidos_o_motivo_e_dito_mas_o_rotulo_continua_valendo()
    {
        // Como o GitHub: nenhum item traz vínculo; o rótulo ainda vale.
        var metric = Blocked(Item("A", labels: ["blocked"]), Item("B"));

        Assert.Equal(1, metric.Value);
        Assert.Equal(0, metric.LinksRead);
        Assert.Contains("não foram lidos nesta fonte", metric.Basis);
    }

    [Fact]
    public void Com_vinculos_lidos_a_base_diz_quantos_itens_foram_lidos()
    {
        var metric = Blocked(Item("A", blockedBy: []), Item("B"), Item("C", blockedBy: ["X-1"]));

        Assert.Contains("Vínculos lidos em 2 de 3 itens", metric.Basis);
    }

    [Fact]
    public void Sem_itens_o_indicador_diz_que_nao_ha_itens()
    {
        var flow = FlowCalculator.Compute(DataKinds.Issues, [], Now, 8);

        Assert.Equal(FlowCalculator.NoItems, flow.Blocked.Unavailable);
        Assert.Null(flow.Blocked.Value);
    }

    [Fact]
    public void O_bloqueio_deixou_de_constar_como_nao_calculado()
    {
        Assert.DoesNotContain(ManagementService.NotCalculatedYet, item => item.Id == "blockers");
        Assert.Contains(ManagementService.NotCalculatedYet, item => item.Id == "scopeChange");
    }

    // ------------------------------------------------------------------ Risco

    private static ProjectRisk? Risk(params WorkItem[] items) =>
        RiskRules.Evaluate([new KeyValuePair<string, List<WorkItem>>(DataKinds.Issues, items.ToList())], Now)
            .FirstOrDefault(risk => risk.Id == "blocked");

    [Fact]
    public void Sem_item_bloqueado_o_risco_nao_dispara()
    {
        Assert.Null(Risk(Item("A"), Item("B", blockedBy: [])));
    }

    [Theory]
    [InlineData(1, RiskSeverity.Low)]
    [InlineData(2, RiskSeverity.Medium)]
    [InlineData(4, RiskSeverity.Medium)]
    [InlineData(5, RiskSeverity.High)]
    public void A_gravidade_sobe_com_a_quantidade_de_itens_bloqueados(int blocked, RiskSeverity expected)
    {
        var risk = Risk(Enumerable.Range(1, blocked).Select(n => Item("B" + n, labels: ["blocked"])).ToArray());

        Assert.Equal(expected, risk!.Severity);
    }

    [Fact]
    public void O_risco_conta_por_vinculo_e_por_rotulo_e_lista_os_itens_do_mais_antigo_ao_mais_novo()
    {
        var risk = Risk(
            Item("novo", labels: ["blocked"], createdDaysAgo: 2),
            Item("antigo", blockedBy: ["X-1"], createdDaysAgo: 40),
            Item("ambos", labels: ["blocked"], blockedBy: ["X-2"], createdDaysAgo: 20));

        Assert.Equal(["antigo", "ambos", "novo"], risk!.Items.Select(item => item.ExternalId));
        Assert.Contains("3 itens abertos estão bloqueados: 2 por vínculo e 2 por rótulo", risk.Evidence[0]);
        Assert.Equal("rule", risk.Origin);
    }

    [Fact]
    public void O_risco_lista_no_maximo_dez_itens()
    {
        var risk = Risk(Enumerable.Range(1, 14).Select(n => Item("B" + n, labels: ["blocked"])).ToArray());

        Assert.Equal(RiskRules.MaxItems, risk!.Items.Count);
        Assert.Contains("14 itens abertos estão bloqueados", risk.Evidence[0]);
    }

    [Fact]
    public void A_regra_consta_entre_as_conferidas()
    {
        Assert.Contains("blocked", RiskRules.All);
    }

    // ------------------------------------------------------------------ Leitura do Jira

    private static JsonObject Link(string typeName, string inward, string direction, string key, string category) => new()
    {
        ["type"] = new JsonObject { ["name"] = typeName, ["inward"] = inward, ["outward"] = "blocks" },
        [direction] = new JsonObject
        {
            ["key"] = key,
            ["fields"] = new JsonObject
            {
                ["status"] = new JsonObject { ["name"] = "x", ["statusCategory"] = new JsonObject { ["key"] = category } }
            }
        }
    };

    private static async Task<WorkItem> SyncedJiraItem(IntegrationHarness h, JsonObject issue)
    {
        h.Jira.Issues.Add(issue);
        var jira = await h.Hub.Projects.LinkAsync(h.ProjectId, (await h.ConnectJiraAsync()).Id, "APP", [DataKinds.Issues], CancellationToken.None);
        await h.SyncAsync(jira);
        return h.Hub.Projects.Data(jira).Items.Values.Single().Item;
    }

    private static JsonObject JiraIssue(JsonNode? links)
    {
        var issue = FakeJira.Issue("APP-1", "2026-11-08T15:30:00.000-0300", "indeterminate", "Em andamento");
        if (links is not null) issue["fields"]!["issuelinks"] = links;
        return issue;
    }

    [Fact]
    public async Task O_jira_pede_os_vinculos_na_busca()
    {
        using var h = new IntegrationHarness();

        await SyncedJiraItem(h, JiraIssue(new JsonArray()));

        Assert.Contains(h.Transport.Requests, request => request.Url.Query.Contains("issuelinks"));
    }

    [Fact]
    public async Task Vinculo_is_blocked_by_com_item_aberto_vira_bloqueio()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJiraItem(h, JiraIssue(new JsonArray(Link("Blocks", "is blocked by", "inwardIssue", "APP-9", "indeterminate"))));

        Assert.Equal(["APP-9"], item.BlockedBy);
    }

    [Fact]
    public async Task Vinculo_com_o_bloqueador_ja_concluido_nao_bloqueia()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJiraItem(h, JiraIssue(new JsonArray(Link("Blocks", "is blocked by", "inwardIssue", "APP-9", "done"))));

        Assert.Empty(item.BlockedBy!);
    }

    [Fact]
    public async Task Item_que_bloqueia_os_outros_nao_esta_bloqueado()
    {
        using var h = new IntegrationHarness();

        // Aqui o item é o que bloqueia (o vínculo aparece como outwardIssue): ele não está bloqueado.
        var item = await SyncedJiraItem(h, JiraIssue(new JsonArray(Link("Blocks", "is blocked by", "outwardIssue", "APP-9", "indeterminate"))));

        Assert.Empty(item.BlockedBy!);
    }

    [Fact]
    public async Task Outro_tipo_de_vinculo_nao_e_bloqueio()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJiraItem(h, JiraIssue(new JsonArray(Link("Relates", "relates to", "inwardIssue", "APP-9", "indeterminate"))));

        Assert.Empty(item.BlockedBy!);
    }

    [Fact]
    public async Task Tipo_renomeado_ainda_conta_se_a_descricao_de_entrada_e_is_blocked_by()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJiraItem(h, JiraIssue(new JsonArray(Link("Impedimento", "is blocked by", "inwardIssue", "APP-9", "indeterminate"))));

        Assert.Equal(["APP-9"], item.BlockedBy);
    }

    [Fact]
    public async Task Sem_o_campo_de_vinculos_o_bloqueio_nao_foi_lido_e_nao_vira_lista_vazia()
    {
        using var h = new IntegrationHarness();

        var item = await SyncedJiraItem(h, JiraIssue(links: null));

        Assert.Null(item.BlockedBy);
    }

    [Fact]
    public async Task O_github_nao_tem_vinculos_mas_o_rotulo_vale()
    {
        using var h = new IntegrationHarness();
        var issue = FakeGitHub.Issue(1, "2026-11-08T10:00:00Z");
        issue["labels"] = new JsonArray(new JsonObject { ["name"] = "blocked" });
        h.GitHub.Issues.Add(issue);
        var link = await h.LinkGitHubAsync(DataKinds.Issues);
        await h.SyncAsync(link);

        var item = h.Hub.Projects.Data(link).Items.Values.Single().Item;

        Assert.Null(item.BlockedBy);
        Assert.True(DeliveryRules.IsBlockedByLabel(item));
    }
}
