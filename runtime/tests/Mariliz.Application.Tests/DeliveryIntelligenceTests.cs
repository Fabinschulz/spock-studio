using System.Text.Json.Nodes;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// Previsão de entrega e riscos por regra. Os históricos são montados semana a semana para que cada cenário e cada
/// risco esperado possa ser conferido à mão.
/// </summary>
public class DeliveryIntelligenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static WorkItem Item(
        string id,
        string category,
        DateTimeOffset created,
        DateTimeOffset? closed = null,
        string kind = DataKinds.Issues,
        DateOnly? due = null,
        DateTimeOffset? updated = null,
        IReadOnlyList<string>? blockedBy = null) =>
        new(kind, id, "Item " + id, kind == DataKinds.Issues ? "Issue" : "Pull request",
            category == StatusCategories.Done ? (kind == DataKinds.PullRequests ? "merged" : "completed") : "open", category, [],
            [], created, updated ?? closed ?? created, closed, due, null, "https://example.test/" + id, [], blockedBy);

    /// <summary>Entregas por semana (da mais antiga para a mais recente, terminando agora) e itens abertos criados antes do histórico.</summary>
    private static List<WorkItem> History(int[] deliveredPerWeek, int open, string kind = DataKinds.Issues)
    {
        var items = new List<WorkItem>();
        var weeks = deliveredPerWeek.Length;
        for (var week = 0; week < weeks; week++)
            for (var n = 0; n < deliveredPerWeek[week]; n++)
            {
                var closed = Now.AddDays(-7 * (weeks - week) + 3 + n * 0.1);
                items.Add(Item($"d{week}-{n}", StatusCategories.Done, closed.AddDays(-1), closed, kind));
            }
        for (var n = 0; n < open; n++) items.Add(Item($"o{n}", StatusCategories.Todo, Now.AddDays(-120), kind: kind));
        return items;
    }

    // ------------------------------------------------------------------ Previsão

    [Fact]
    public void Ritmo_constante_da_a_mesma_data_nos_tres_cenarios_e_confianca_alta()
    {
        var forecast = ForecastCalculator.Compute(DataKinds.Issues, History(Enumerable.Repeat(2, 12).ToArray(), open: 10), Now, true);

        // 10 itens a 2 por semana: toda simulação termina na 5ª semana.
        Assert.Null(forecast.Unavailable);
        Assert.Equal((10, 12, ForecastCalculator.Trials), (forecast.Remaining, forecast.HistoryWeeks, forecast.Trials));
        Assert.All(forecast.Scenarios, scenario => Assert.Equal(5, scenario.Weeks));
        Assert.Equal(new DateOnly(2026, 12, 15), forecast.Scenarios[1].By);
        Assert.Equal(["optimistic", "likely", "conservative"], forecast.Scenarios.Select(s => s.Id));
        Assert.Equal([15, 50, 85], forecast.Scenarios.Select(s => s.Percentile));
        Assert.Equal(ForecastConfidence.High, forecast.Confidence);
        Assert.Empty(forecast.ConfidenceReasons);
        Assert.Empty(forecast.RiskFactors);
    }

    [Fact]
    public void Ritmo_variavel_ordena_os_cenarios_repete_o_resultado_e_baixa_a_confianca_com_motivo()
    {
        // 5 semanas zeradas (mais de um terço) e variação de 0,94 (abaixo do limite de 1,0): cai um nível.
        int[] weekly = [0, 4, 2, 0, 4, 2, 0, 4, 2, 0, 4, 0];
        var items = History(weekly, open: 30);

        var first = ForecastCalculator.Compute(DataKinds.Issues, items, Now, true);
        var second = ForecastCalculator.Compute(DataKinds.Issues, items, Now, true);
        var more = ForecastCalculator.Compute(DataKinds.Issues, History(weekly, open: 60), Now, true);

        Assert.Equal(first.Scenarios, second.Scenarios);
        var weeks = first.Scenarios.Select(s => s.Weeks).ToList();
        Assert.True(weeks[0] <= weeks[1] && weeks[1] <= weeks[2], string.Join(",", weeks));
        Assert.True(weeks[2] > weeks[0], "Com ritmo variável, o conservador fica depois do otimista.");
        Assert.True(more.Scenarios[1].Weeks > first.Scenarios[1].Weeks);
        Assert.Equal(weekly, first.WeeklyThroughput);
        Assert.Equal(ForecastConfidence.Medium, first.Confidence);
        Assert.Equal(["5 de 12 semanas sem nenhuma entrega."], first.ConfidenceReasons);
    }

    [Fact]
    public void Fonte_incompleta_e_escopo_crescendo_baixam_a_confianca_e_viram_fator_de_risco()
    {
        var items = History(Enumerable.Repeat(2, 12).ToArray(), open: 10);
        for (var n = 0; n < 30; n++) items.Add(Item($"novo{n}", StatusCategories.Todo, Now.AddDays(-n - 1)));

        var forecast = ForecastCalculator.Compute(DataKinds.Issues, items, Now, sourcesComplete: false);

        Assert.Equal(ForecastConfidence.Low, forecast.Confidence);
        Assert.Contains("Ao menos uma fonte teve a última sincronização incompleta.", forecast.ConfidenceReasons);
        Assert.Contains(forecast.RiskFactors, factor => factor.Contains("chegaram 54 itens e saíram 24"));
    }

    [Theory]
    [InlineData(0, 12, 2, "Não há itens abertos: nada a prever.")]
    [InlineData(10, 5, 2, "São precisas ao menos 6 semanas de histórico; os dados cobrem 5.")]
    [InlineData(10, 12, 0, "São precisos ao menos 5 itens entregues nas últimas 12 semanas; há 0.")]
    public void Sem_base_suficiente_nao_ha_previsao(int open, int historyWeeks, int perWeek, string reason)
    {
        var items = History(Enumerable.Repeat(perWeek, historyWeeks).ToArray(), open: 0);
        for (var n = 0; n < open; n++) items.Add(Item($"o{n}", StatusCategories.Todo, Now.AddDays(-7 * historyWeeks - 1)));
        if (items.Count == 0) items.Add(Item("x", StatusCategories.Done, Now.AddDays(-7 * historyWeeks - 1), Now.AddDays(-1)));

        var forecast = ForecastCalculator.Compute(DataKinds.Issues, items, Now, true);

        Assert.Equal(reason, forecast.Unavailable);
        Assert.Empty(forecast.Scenarios);
        Assert.Null(forecast.Confidence);
    }

    [Fact]
    public void Ritmo_que_nao_conclui_no_horizonte_nao_vira_data()
    {
        int[] weekly = [1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1];
        var forecast = ForecastCalculator.Compute(DataKinds.Issues, History(weekly, open: 2000), Now, true);

        Assert.Equal("No ritmo observado, parte relevante das simulações não conclui em 5 anos.", forecast.Unavailable);
        Assert.Empty(forecast.Scenarios);
    }

    // ------------------------------------------------------------------ Riscos

    private static List<KeyValuePair<string, List<WorkItem>>> ByKind(IEnumerable<WorkItem> items) =>
        items.GroupBy(i => i.Kind).Select(g => new KeyValuePair<string, List<WorkItem>>(g.Key, g.ToList())).ToList();

    [Fact]
    public void Cada_regra_dispara_com_as_evidencias_e_os_itens_que_a_sustentam()
    {
        var items = new List<WorkItem>
        {
            Item("A-1", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1)),
            Item("A-2", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 20)),
            Item("A-3", StatusCategories.InProgress, Now.AddDays(-40), updated: Now.AddDays(-20)),
            Item("A-4", StatusCategories.InProgress, Now.AddDays(-40), updated: Now.AddDays(-2)),
            Item("7", StatusCategories.InProgress, Now.AddDays(-10), kind: DataKinds.PullRequests),
            Item("8", StatusCategories.InProgress, Now.AddDays(-3), kind: DataKinds.PullRequests)
        };

        var risks = RiskRules.Evaluate(ByKind(items), Now);

        var overdue = risks.Single(r => r.Id == "overdue");
        Assert.Equal(RiskSeverity.High, overdue.Severity);
        Assert.Equal(["1 de 2 itens abertos com prazo está vencido.", "O mais atrasado venceu há 9 dias."], overdue.Evidence);
        Assert.Equal("A-1", Assert.Single(overdue.Items).ExternalId);
        Assert.Equal(("A-3", RiskSeverity.Low), (Assert.Single(risks.Single(r => r.Id == "stalled-wip").Items).ExternalId,
            risks.Single(r => r.Id == "stalled-wip").Severity));
        Assert.Equal("7", Assert.Single(risks.Single(r => r.Id == "review-queue").Items).ExternalId);
        Assert.All(risks, risk => Assert.Equal("rule", risk.Origin));
        Assert.Equal(RiskSeverity.High, risks[0].Severity);
    }

    [Fact]
    public void Escopo_crescendo_e_queda_de_ritmo_comparam_periodos_e_dizem_a_tendencia()
    {
        int[] weekly = [2, 2, 2, 2, 0, 1, 0, 0];
        var items = History(weekly, open: 0);
        for (var n = 0; n < 8; n++) items.Add(Item($"novo{n}", StatusCategories.Todo, Now.AddDays(-n - 1)));

        var risks = RiskRules.Evaluate(ByKind(items), Now);

        var drop = risks.Single(r => r.Id == "throughput-drop:issues");
        Assert.Equal(RiskSeverity.Medium, drop.Severity);
        Assert.Equal("Caiu de 8 para 1 entregas por período de 4 semanas.", drop.Trend);
        var growth = risks.Single(r => r.Id == "scope-growth:issues");
        Assert.Equal(RiskSeverity.High, growth.Severity);
        Assert.Equal(["Nas últimas 4 semanas chegaram 9 e foram entregues 1."], growth.Evidence);
    }

    [Fact]
    public void Escopo_crescendo_tem_dois_niveis_de_proposito_a_previsao_avisa_antes_e_a_lista_de_riscos_so_quando_e_forte()
    {
        // 12 semanas: 24 entregas e 30 chegadas (+25%): passa do nível de atenção da previsão (20%), mas não do de risco (50%).
        // As 6 chegadas a mais são de 5 a 6 semanas atrás, fora das últimas 4 semanas que a regra de risco compara.
        var items = History(Enumerable.Repeat(2, 12).ToArray(), open: 10);
        for (var n = 0; n < 6; n++) items.Add(Item($"novo{n}", StatusCategories.Todo, Now.AddDays(-35 - n)));

        var forecast = ForecastCalculator.Compute(DataKinds.Issues, items, Now, sourcesComplete: true);
        var risks = RiskRules.Evaluate(ByKind(items), Now);

        Assert.Contains(forecast.RiskFactors, factor => factor.Contains("chegaram 30 itens e saíram 24"));
        Assert.Contains("Chegaram mais itens do que saíram no período: o escopo está crescendo.", forecast.ConfidenceReasons);
        Assert.DoesNotContain(risks, risk => risk.Id.StartsWith("scope-growth", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(12, 10, false, false)]   // diferença de 2 itens: oscilação normal nos dois níveis
    [InlineData(13, 10, true, false)]    // +30%: atenção, ainda não é risco
    [InlineData(14, 10, true, false)]    // +40%
    [InlineData(15, 10, true, true)]     // +50%: risco
    [InlineData(5, 0, true, true)]       // nada entregue e chegando itens: os dois níveis
    [InlineData(2, 0, false, false)]     // pouco demais para dizer alguma coisa
    public void Os_dois_niveis_de_escopo_crescendo_ficam_num_lugar_so(int arrivals, int deliveries, bool watch, bool risk)
    {
        Assert.Equal(watch, TrendRules.ScopeGrowing(arrivals, deliveries, TrendRules.ScopeWatchRatio));
        Assert.Equal(risk, TrendRules.ScopeGrowing(arrivals, deliveries, TrendRules.ScopeRiskRatio));
        Assert.True(TrendRules.ScopeWatchRatio < TrendRules.ScopeRiskRatio);
    }

    [Theory]
    [InlineData(4, 2, true)]    // exatamente a metade
    [InlineData(8, 4, true)]
    [InlineData(4, 3, false)]
    [InlineData(3, 0, false)]   // antes poucas entregas: não há de onde cair
    public void Queda_de_ritmo_e_a_metade_ou_menos_com_ao_menos_quatro_entregas_antes(int before, int after, bool dropped) =>
        Assert.Equal(dropped, TrendRules.ThroughputDropped(before, after));

    [Fact]
    public void Dados_saudaveis_nao_geram_risco()
    {
        var items = History(Enumerable.Repeat(2, 8).ToArray(), open: 3);

        Assert.Empty(RiskRules.Evaluate(ByKind(items), Now));
    }

    // ------------------------------------------------------------------ Protocolo e contrato

    private static async Task<IntegrationHarness> Seeded(IEnumerable<WorkItem> items)
    {
        var h = new IntegrationHarness();
        h.Clock.UtcNow = Now;
        var integration = await h.LinkGitHubAsync();
        var store = new FileSyncedDataStore(h.DataDir);
        var data = new SyncedData();
        foreach (var item in items) data.Upsert(item, Now);
        data.Record(new SyncExecution("sync-0000000000aa", Now, Now, SyncStatus.Succeeded, false, 1, data.Items.Count, data.Items.Count, 0, 0, null));
        store.Write(h.ProjectId, integration.Id, data);
        return h;
    }

    [Fact]
    public async Task Previsao_e_riscos_pelo_protocolo_respeitam_o_projeto_e_dizem_quando_nao_ha_dados()
    {
        using var h = await Seeded(History([2, 3, 1, 2, 2, 3, 1, 2], open: 9));

        var forecast = h.Request("management.forecast", new JsonObject { ["projectId"] = h.ProjectId })["result"]!;
        var otherRisks = h.Request("management.risks", new JsonObject { ["projectId"] = h.OtherProjectId })["result"]!;
        var denied = h.Request("management.risks", new JsonObject { ["projectId"] = "p-000000000000" });

        Assert.Equal("likely", forecast["kinds"]![0]!["scenarios"]![1]!["id"]!.GetValue<string>());
        Assert.True(forecast["complete"]!.GetValue<bool>());
        Assert.NotEmpty(forecast["assumptions"]!.AsArray());
        Assert.False(otherRisks["hasData"]!.GetValue<bool>());
        Assert.Empty(otherRisks["risks"]!.AsArray());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Respostas_de_previsao_e_riscos_conferem_com_as_fixtures_do_contrato()
    {
        var items = History([2, 3, 1, 2, 2, 3, 1, 2, 0, 2, 3, 2], open: 9);
        items.Add(Item("A-1", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1)));
        items.Add(Item("7", StatusCategories.InProgress, Now.AddDays(-12), kind: DataKinds.PullRequests));
        // Bloqueado pelo vínculo "is blocked by" do Jira com A-1, que segue aberto: aparece como risco.
        items.Add(Item("A-2", StatusCategories.InProgress, Now.AddDays(-5), blockedBy: ["A-1"]));
        using var h = await Seeded(items);

        var scenarios = new Dictionary<string, JsonNode>
        {
            ["forecast"] = h.Request("management.forecast", new JsonObject { ["projectId"] = h.ProjectId })["result"]!,
            ["risks"] = h.Request("management.risks", new JsonObject { ["projectId"] = h.ProjectId })["result"]!
        };

        foreach (var (name, result) in scenarios)
        {
            var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }).Replace(h.ProjectId, "p-000000000001").ReplaceLineEndings("\n");

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
