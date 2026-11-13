using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public class ReportTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly IntegrationHarness _harness = new();
    private readonly FileRunStore _runs;
    private readonly RiskStore _riskStore = new();
    private readonly DecisionService _decisions;
    private readonly ManagementService _management;
    private readonly EngineSession _session;

    public ReportTests()
    {
        _harness.Clock.UtcNow = Now;
        _runs = new FileRunStore(_harness.DataDir);
        _management = new ManagementService(_harness.Workspace, _harness.Hub, _harness.Clock, _riskStore);
        _decisions = new DecisionService(_harness.Workspace, new FileDecisionStore(_harness.DataDir), _runs,
            id => new EvidencePack(id, "Loja", Now, [new("flow.issues.items", "indicadores", "Issues: 7 itens.")], []), _harness.Audit, _harness.Clock);
        var report = new ReportService(_harness.Workspace, _management, _decisions, _runs, _harness.Clock);
        _session = new EngineSession("test", workspace: _harness.Workspace, integrations: _harness.Hub, management: _management, decisions: _decisions, reports: report);
    }

    public void Dispose() => _harness.Dispose();

    private string ProjectId => _harness.ProjectId;

    private sealed class RiskStore : IRiskHistoryStore
    {
        public Dictionary<string, List<RiskSnapshot>> Data { get; } = [];
        public IReadOnlyList<RiskSnapshot> Load(string projectId) => Data.GetValueOrDefault(projectId) ?? [];
        public void Save(string projectId, IReadOnlyList<RiskSnapshot> snapshots) => Data[projectId] = [.. snapshots];
    }

    private JsonObject Report(int? weeks = null, string? projectId = null)
    {
        var parameters = new JsonObject { ["projectId"] = projectId ?? ProjectId };
        if (weeks is not null) parameters["weeks"] = weeks;
        return _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "management.report", ["params"] = parameters }.ToJsonString()).Response;
    }

    private static string Code(JsonObject response) => response["error"]!["code"]!.GetValue<string>();

    private void SaveRun(string runId, string kind, string state, int daysAgo, string? projectId = null) =>
        _runs.SaveJournal(new RunJournal { RunId = runId, ProjectId = projectId ?? ProjectId, Kind = kind, State = state, StartedAt = Now.AddDays(-daysAgo) });

    private void Decide(string title, int daysAgo, string? projectId = null)
    {
        _harness.Clock.UtcNow = Now.AddDays(-daysAgo);
        _decisions.Record(projectId ?? ProjectId, title, "Decisão de " + title, null, []);
        _harness.Clock.UtcNow = Now;
    }

    private async Task SeedItemsAsync()
    {
        var integration = await _harness.LinkGitHubAsync();
        var data = new SyncedData();
        // Seis itens entregues, o item n levou n dias em andamento (cycle time mediano 3, percentil 85 em 6), o histórico cobre
        // mais de 12 semanas e os quatro primeiros e o item aberto pertencem ao marco v1.0.
        var v1 = new[] { new WorkItemMilestone("v1.0", new DateOnly(2026, 12, 12)) };
        for (var n = 1; n <= 6; n++)
            data.Upsert(new WorkItem(DataKinds.Issues, n.ToString(), "Item " + n, "Issue", "completed", StatusCategories.Done, [], [],
                Now.AddDays(-100), Now.AddDays(-3 * n), Now.AddDays(-3 * n), null, null, "https://example.test/" + n,
                [new WorkItemTransition(Now.AddDays(-3 * n - n), "new", "open"), new WorkItemTransition(Now.AddDays(-3 * n), "open", "completed")],
                null, n <= 4 ? v1 : []), Now);
        data.Upsert(new WorkItem(DataKinds.Issues, "7", "Item 7", "Issue", "open", StatusCategories.InProgress, [], ["blocked"],
            Now.AddDays(-4), Now.AddDays(-1), null, null, null, "https://example.test/7", [], null, v1), Now);
        data.Record(new SyncExecution("sync-0000000000aa", Now, Now, SyncStatus.Succeeded, false, 1, data.Items.Count, data.Items.Count, 0, 0, null));
        new FileSyncedDataStore(_harness.DataDir).Write(ProjectId, integration.Id, data);
    }

    [Fact]
    public void So_entram_as_decisoes_e_execucoes_do_periodo_conferido_a_mao()
    {
        Decide("recente", daysAgo: 10);          // dentro de 4 semanas (28 dias)
        Decide("antiga", daysAgo: 40);           // fora de 4 semanas, dentro de 8
        SaveRun("run-a", RunKinds.Apply, RunStates.Completed, daysAgo: 3);
        SaveRun("run-b", RunKinds.Apply, RunStates.Completed, daysAgo: 60);   // fora de 8 semanas (56 dias)

        var four = Report(4)["result"]!;
        var eight = Report(8)["result"]!;

        Assert.Equal(["recente"], four["decisions"]!.AsArray().Select(d => d!["title"]!.GetValue<string>()).ToArray());
        Assert.Equal(["recente", "antiga"], eight["decisions"]!.AsArray().Select(d => d!["title"]!.GetValue<string>()).ToArray());
        Assert.Equal(1, four["runs"]![0]!["count"]!.GetValue<int>());
        Assert.Equal(1, eight["runs"]![0]!["count"]!.GetValue<int>());
    }

    [Fact]
    public void As_execucoes_vem_contadas_por_tipo_e_estado_em_ordem_fixa()
    {
        SaveRun("r1", RunKinds.Apply, RunStates.Completed, 1);
        SaveRun("r2", RunKinds.Apply, RunStates.Completed, 2);
        SaveRun("r3", RunKinds.Apply, RunStates.Completed, 3);
        SaveRun("r4", RunKinds.Apply, RunStates.Failed, 4);
        SaveRun("r5", RunKinds.Workflow, RunStates.Completed, 5);
        SaveRun("r6", RunKinds.Workflow, RunStates.Completed, 6);

        var runs = Report()["result"]!["runs"]!.AsArray().Select(r => $"{r!["kind"]!.GetValue<string>()}/{r["state"]!.GetValue<string>()}={r["count"]!.GetValue<int>()}").ToArray();

        Assert.Equal(["apply/completed=3", "apply/failed=1", "workflow/completed=2"], runs);
    }

    [Fact]
    public void As_mudancas_de_risco_do_periodo_vem_da_mais_recente_e_so_as_que_aconteceram_nele()
    {
        RiskSnapshotEntry Risk(string severity) => new("blocked", "Itens bloqueados", severity, ["1"]);
        _riskStore.Data[ProjectId] =
        [
            new RiskSnapshot(Now.AddDays(-30), Now.AddDays(-30), [Risk("low")]),     // fora de 2 semanas
            new RiskSnapshot(Now.AddDays(-9), Now.AddDays(-9), [Risk("medium")]),    // dentro: subiu
            new RiskSnapshot(Now.AddDays(-2), Now.AddDays(-2), [])                   // dentro: resolvido
        ];

        var two = Report(2)["result"]!["riskChanges"]!.AsArray();
        var eight = Report(8)["result"]!["riskChanges"]!.AsArray();

        Assert.Equal(["resolved", "raised"], two.Select(c => c!["change"]!["kind"]!.GetValue<string>()).ToArray());
        Assert.Equal(["resolved", "raised", "observed"], eight.Select(c => c!["change"]!["kind"]!.GetValue<string>()).ToArray());
        Assert.Equal("Itens bloqueados", two[0]!["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task O_relatorio_compoe_as_mesmas_respostas_dos_metodos_de_gestao_no_periodo_pedido()
    {
        await SeedItemsAsync();

        var result = Report(4)["result"]!;
        var metrics = _management.Metrics(ProjectId, 4);

        Assert.Equal(4, result["weeks"]!.GetValue<int>());
        Assert.Equal(4, result["metrics"]!["periodWeeks"]!.GetValue<int>());
        Assert.Equal(metrics.Kinds[0].Throughput.Total, result["metrics"]!["kinds"]![0]!["throughput"]!["total"]!.GetValue<int>());
        Assert.Equal(_management.Risks(ProjectId).Risks.Count, result["risks"]!["risks"]!.AsArray().Count);
        Assert.Equal(Path.GetFileName(_harness.Project.Root), result["projectName"]!.GetValue<string>());
        Assert.Equal(_management.Forecast(ProjectId).Kinds.Count, result["forecast"]!["kinds"]!.AsArray().Count);
    }

    [Fact]
    public void O_periodo_padrao_e_de_quatro_semanas_e_termina_agora()
    {
        var result = Report()["result"]!;

        Assert.Equal(4, result["weeks"]!.GetValue<int>());
        Assert.Equal(Now, result["periodTo"]!.GetValue<DateTimeOffset>());
        Assert.Equal(Now.AddDays(-28), result["periodFrom"]!.GetValue<DateTimeOffset>());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(27)]
    [InlineData(0)]
    public void Periodo_fora_de_duas_a_vinte_e_seis_semanas_e_recusado(int weeks)
    {
        Assert.Equal("INVALID_PARAMS", Code(Report(weeks)));
    }

    [Fact]
    public void Projeto_nao_autorizado_e_recusado_e_um_projeto_nao_ve_o_que_e_do_outro()
    {
        Decide("só do primeiro", 5);
        SaveRun("run-1", RunKinds.Apply, RunStates.Completed, 2);

        var other = Report(projectId: _harness.OtherProjectId)["result"]!;

        Assert.Equal("PROJECT_NOT_AUTHORIZED", Code(Report(projectId: "p-desconhecido01")));
        Assert.Empty(other["decisions"]!.AsArray());
        Assert.Empty(other["runs"]!.AsArray());
    }

    [Fact]
    public void A_base_diz_que_nada_foi_escrito_por_modelo_e_o_que_o_historico_nao_cobre()
    {
        var basis = Report()["result"]!["basis"]!.GetValue<string>();

        Assert.Contains("nenhum texto foi escrito por modelo de linguagem", basis);
        Assert.Contains("O histórico de riscos só existe desde o primeiro registro", basis);
    }

    [Fact]
    public async Task Gerar_o_relatorio_nao_escreve_nada_em_disco()
    {
        await SeedItemsAsync();
        Decide("uma", 3);
        SaveRun("run-1", RunKinds.Apply, RunStates.Completed, 2);
        string[] Files() => Directory.EnumerateFiles(_harness.DataDir, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).Select(file => $"{file}|{new FileInfo(file).Length}|{File.GetLastWriteTimeUtc(file):O}").ToArray();
        var before = Files();

        Report(4);
        Report(12);

        Assert.Equal(before, Files());
        Assert.Empty(_riskStore.Data);
    }

    [Fact]
    public async Task O_relatorio_leva_o_titulo_das_decisoes_mas_nunca_os_fatos_guardados_nelas()
    {
        await SeedItemsAsync();
        Decide("Adiar a release", 5);

        var text = Report(4)["result"]!["decisions"]!.ToJsonString();

        Assert.Contains("Adiar a release", text);
        Assert.DoesNotContain("Issues: 7 itens.", text);
        Assert.DoesNotContain("\"facts\":[", text);
    }

    [Fact]
    public async Task Fixture_de_contrato_do_relatorio()
    {
        await SeedItemsAsync();
        Decide("Adiar a release", 10);
        SaveRun("run-1", RunKinds.Apply, RunStates.Completed, 3);
        SaveRun("run-2", RunKinds.Workflow, RunStates.Failed, 6);
        _management.CaptureRisks(ProjectId);

        var text = Report(4)["result"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(ProjectId, "p-000000000001").Replace(Path.GetFileName(_harness.Project.Root), "loja").ReplaceLineEndings("\n");
        text = Regex.Replace(text, "dec-[0-9a-f]{12}", "dec-000000000001");
        text = Regex.Replace(text, "pint-[0-9a-f]{12}", "pint-000000000001");

        var path = ContractFiles.Path_("fixtures", "management", "report.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "report: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
