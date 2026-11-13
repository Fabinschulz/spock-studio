using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public class DecisionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly IntegrationHarness _harness = new();
    private readonly FileRunStore _runs;
    private readonly FileDecisionStore _store;
    private readonly DecisionService _service;
    private readonly EngineSession _session;
    private List<EvidenceFact> _facts = [new("flow.issues.items", "indicadores", "Issues: 18 itens sincronizados, 2 abertos."), new("risk.overdue", "riscos", "1 item vencido.")];

    public DecisionTests()
    {
        _harness.Clock.UtcNow = Now;
        _runs = new FileRunStore(_harness.DataDir);
        _store = new FileDecisionStore(_harness.DataDir);
        _service = new DecisionService(_harness.Workspace, _store, _runs,
            projectId => new EvidencePack(projectId, "Loja", Now, _facts, ["Nenhum código é enviado."]), _harness.Audit, _harness.Clock);
        _session = new EngineSession("test", workspace: _harness.Workspace, integrations: _harness.Hub, decisions: _service);
        SaveRun("run-1", RunKinds.Apply, RunStates.Completed);
    }

    public void Dispose() => _harness.Dispose();

    private string ProjectId => _harness.ProjectId;

    private void SaveRun(string runId, string kind, string state, string? projectId = null) =>
        _runs.SaveJournal(new RunJournal { RunId = runId, ProjectId = projectId ?? ProjectId, Kind = kind, State = state, StartedAt = Now.AddHours(-2) });

    private JsonObject Call(string method, JsonObject parameters) =>
        _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    private JsonObject Record(string title = "Adiar a release", string decision = "Adiar a release 1.0 em uma semana.", string? rationale = "Três itens bloqueados.",
        string[]? runs = null, string? projectId = null) =>
        Call("decision.record", new JsonObject
        {
            ["projectId"] = projectId ?? ProjectId, ["title"] = title, ["decision"] = decision, ["rationale"] = rationale,
            ["runIds"] = new JsonArray((runs ?? []).Select(id => (JsonNode)JsonValue.Create(id)!).ToArray())
        });

    private static string Code(JsonObject response) => response["error"]!["code"]!.GetValue<string>();

    [Fact]
    public void Registra_o_texto_aparado_com_o_instante_e_os_fatos_do_motor_naquele_momento()
    {
        var response = Record("  Adiar a release  ", "  Adiar a release 1.0 em uma semana.  ", "  Três itens bloqueados.  ");

        var record = response["result"]!["decision"]!;
        Assert.Equal("Adiar a release", record["title"]!.GetValue<string>());
        Assert.Equal("Adiar a release 1.0 em uma semana.", record["decision"]!.GetValue<string>());
        Assert.Equal("Três itens bloqueados.", record["rationale"]!.GetValue<string>());
        Assert.StartsWith("dec-", record["id"]!.GetValue<string>());
        Assert.Equal(["flow.issues.items", "risk.overdue"], record["facts"]!.AsArray().Select(f => f!["id"]!.GetValue<string>()).ToArray());
        Assert.Equal("Nenhum código é enviado.", record["withheld"]![0]!.GetValue<string>());
        Assert.False(response["result"]!["redacted"]!.GetValue<bool>());
    }

    [Fact]
    public void Os_fatos_ficam_como_eram_no_dia_mesmo_que_o_motor_passe_a_dizer_outra_coisa()
    {
        var id = Record()["result"]!["decision"]!["id"]!.GetValue<string>();

        _facts = [new("flow.issues.items", "indicadores", "Issues: 40 itens sincronizados, 30 abertos.")];
        var reopened = Call("decision.get", new JsonObject { ["projectId"] = ProjectId, ["decisionId"] = id })["result"]!["decision"]!;

        Assert.Equal("Issues: 18 itens sincronizados, 2 abertos.", reopened["facts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Os_fatos_nunca_vem_do_pedido()
    {
        var response = Call("decision.record", new JsonObject
        {
            ["projectId"] = ProjectId, ["title"] = "Decisão", ["decision"] = "Seguir.",
            ["facts"] = new JsonArray(new JsonObject { ["id"] = "inventado", ["group"] = "x", ["text"] = "Fato inventado." })
        });

        var facts = response["result"]!["decision"]!["facts"]!.AsArray();
        Assert.DoesNotContain(facts, fact => fact!["id"]!.GetValue<string>() == "inventado");
    }

    [Fact]
    public void Liga_execucoes_existentes_do_projeto_com_o_tipo_o_estado_e_o_inicio_de_quando_foi_decidido()
    {
        SaveRun("run-2", RunKinds.Workflow, RunStates.Failed);

        var record = Record(runs: ["run-1", "run-2", "run-1"])["result"]!["decision"]!;

        var runs = record["runs"]!.AsArray();
        Assert.Equal(["run-1", "run-2"], runs.Select(run => run!["runId"]!.GetValue<string>()).ToArray());
        Assert.Equal(("workflow", "failed"), (runs[1]!["kind"]!.GetValue<string>(), runs[1]!["state"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData("run-inexistente")]
    [InlineData("../run-1")]
    [InlineData("a/b")]
    public void Execucao_que_nao_existe_ou_com_id_estranho_recusa_o_registro_inteiro(string runId)
    {
        var response = Record(runs: ["run-1", runId]);

        Assert.Equal("DECISION_RUN_NOT_FOUND", Code(response));
        Assert.Empty(_service.List(ProjectId));
    }

    [Fact]
    public void Execucao_de_outro_projeto_nao_se_liga()
    {
        SaveRun("run-outro", RunKinds.Apply, RunStates.Completed, _harness.OtherProjectId);

        Assert.Equal("DECISION_RUN_NOT_FOUND", Code(Record(runs: ["run-outro"])));
    }

    [Fact]
    public void Liga_a_no_maximo_dez_execucoes()
    {
        for (var n = 0; n < 11; n++) SaveRun("run-x" + n, RunKinds.Apply, RunStates.Completed);

        Assert.Equal("INVALID_PARAMS", Code(Record(runs: Enumerable.Range(0, 11).Select(n => "run-x" + n).ToArray())));
        Assert.Empty(Assert.IsType<JsonArray>(_service.List(ProjectId)));
    }

    [Theory]
    [InlineData("", "Seguir.")]
    [InlineData("   ", "Seguir.")]
    [InlineData("Título", "")]
    [InlineData("Título", "   ")]
    public void Titulo_e_decisao_nao_podem_ficar_em_branco(string title, string decision)
    {
        Assert.Equal("INVALID_PARAMS", Code(Record(title, decision)));
    }

    [Fact]
    public void Os_tamanhos_tem_limite_e_o_motivo_em_branco_vira_nulo()
    {
        Assert.Equal("INVALID_PARAMS", Code(Record(title: new string('t', DecisionService.MaxTitle + 1))));
        Assert.Equal("INVALID_PARAMS", Code(Record(decision: new string('d', DecisionService.MaxDecision + 1))));
        Assert.Equal("INVALID_PARAMS", Code(Record(rationale: new string('r', DecisionService.MaxRationale + 1))));

        var blank = Record(rationale: "   ")["result"]!["decision"]!;
        Assert.Null(blank["rationale"]);
        var limit = Record(title: new string('t', DecisionService.MaxTitle), decision: new string('d', DecisionService.MaxDecision))["result"];
        Assert.NotNull(limit);
    }

    [Fact]
    public void Um_segredo_colado_no_texto_nao_fica_gravado_e_a_resposta_avisa_que_houve_redacao()
    {
        var response = Record(decision: "Usar a chave AKIAIOSFODNN7EXAMPLE do ambiente de produção.", rationale: "token ghp_abcdefghijklmnopqrstuvwxyz0123456789");

        Assert.True(response["result"]!["redacted"]!.GetValue<bool>());
        var id = response["result"]!["decision"]!["id"]!.GetValue<string>();
        var onDisk = File.ReadAllText(Path.Combine(_harness.DataDir, "projects", ProjectId, "decisions", id + ".json"));
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", onDisk);
        Assert.DoesNotContain("ghp_abcdefghijklmnopqrstuvwxyz0123456789", onDisk);
    }

    [Fact]
    public void A_trilha_de_auditoria_leva_numeros_e_identificadores_nunca_o_texto_da_decisao()
    {
        Record("Título secreto da decisão", "Texto secreto da decisão.", "Motivo secreto.", runs: ["run-1"]);

        var audit = Assert.Single(_harness.Audit.Entries, entry => entry.Action == "decision.recorded");
        Assert.DoesNotContain("secreto", audit.Detail);
        Assert.Contains("\"runs\":1", audit.Detail);
        Assert.Contains("\"facts\":2", audit.Detail);
    }

    [Fact]
    public void Um_projeto_guarda_no_maximo_quinhentas_decisoes()
    {
        var store = new FullStore(DecisionService.MaxPerProject);
        var service = new DecisionService(_harness.Workspace, store, _runs, id => new EvidencePack(id, "Loja", Now, [], []), _harness.Audit, _harness.Clock);

        var error = Assert.Throws<EngineException>(() => service.Record(ProjectId, "T", "D", null, []));

        Assert.Equal("DECISION_LIMIT", error.Code);
    }

    private sealed class FullStore(int count) : IDecisionStore
    {
        public IReadOnlyList<ManagementDecision> List(string projectId) => Enumerable.Range(0, count)
            .Select(n => new ManagementDecision("dec-" + n.ToString("x12"), projectId, Now, "T", "D", null, [], [], [])).ToList();

        public ManagementDecision? Get(string projectId, string decisionId) => null;
        public void Save(ManagementDecision decision) => throw new InvalidOperationException("Não devia gravar.");
    }

    [Fact]
    public void A_lista_vem_da_mais_recente_para_a_mais_antiga_sem_os_fatos_mas_com_a_contagem()
    {
        Record("Primeira");
        _harness.Clock.UtcNow = Now.AddDays(1);
        Record("Segunda", runs: ["run-1"]);

        var list = Call("decision.list", new JsonObject { ["projectId"] = ProjectId })["result"]!["decisions"]!.AsArray();

        Assert.Equal(["Segunda", "Primeira"], list.Select(item => item!["title"]!.GetValue<string>()).ToArray());
        Assert.Equal(2, list[0]!["factCount"]!.GetValue<int>());
        Assert.Null(list[0]!["facts"]);
        Assert.Equal("run-1", list[0]!["runs"]![0]!["runId"]!.GetValue<string>());
    }

    [Fact]
    public void Um_projeto_nao_ve_nem_abre_as_decisoes_do_outro_e_o_nao_autorizado_e_recusado()
    {
        var id = Record()["result"]!["decision"]!["id"]!.GetValue<string>();

        Assert.Empty(Call("decision.list", new JsonObject { ["projectId"] = _harness.OtherProjectId })["result"]!["decisions"]!.AsArray());
        Assert.Equal("DECISION_NOT_FOUND", Code(Call("decision.get", new JsonObject { ["projectId"] = _harness.OtherProjectId, ["decisionId"] = id })));
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Code(Call("decision.list", new JsonObject { ["projectId"] = "p-desconhecido01" })));
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Code(Record(projectId: "p-desconhecido01")));
    }

    [Theory]
    [InlineData("dec-000000000000")]
    [InlineData("../dec-000000000000")]
    [InlineData("qualquer")]
    public void Decisao_que_nao_existe_ou_com_id_forjado_nao_e_encontrada(string decisionId)
    {
        Assert.Equal("DECISION_NOT_FOUND", Code(Call("decision.get", new JsonObject { ["projectId"] = ProjectId, ["decisionId"] = decisionId })));
    }

    [Fact]
    public void A_decisao_nao_se_edita_nem_se_apaga_porque_o_protocolo_nao_oferece_como()
    {
        Assert.Equal(["decision.record", "decision.list", "decision.get"], ProtocolMethods.Decision);
        Assert.Equal("METHOD_NOT_FOUND", Code(Call("decision.delete", new JsonObject { ["projectId"] = ProjectId, ["decisionId"] = "dec-000000000000" })));
        Assert.Equal("METHOD_NOT_FOUND", Code(Call("decision.update", new JsonObject { ["projectId"] = ProjectId })));
    }

    [Fact]
    public void Um_arquivo_ilegivel_fica_fora_da_lista_sem_derrubar_as_outras()
    {
        var id = Record()["result"]!["decision"]!["id"]!.GetValue<string>();
        File.WriteAllText(Path.Combine(_harness.DataDir, "projects", ProjectId, "decisions", "dec-ffffffffffff.json"), "{ não é json");

        var list = _store.List(ProjectId);

        Assert.Equal(id, Assert.Single(list).Id);
    }

    [Theory]
    [InlineData("../fora", "dec-000000000000")]
    [InlineData("p-000000000001", "../dec-000000000000")]
    [InlineData("", "dec-000000000000")]
    public void Identificador_forjado_nao_aponta_para_fora_da_pasta_de_dados(string projectId, string decisionId)
    {
        Assert.Throws<EngineException>(() => _store.Get(projectId, decisionId));
    }

    [Fact]
    public void Fixtures_de_contrato_do_registro_da_lista_e_da_abertura()
    {
        SaveRun("run-2", RunKinds.Workflow, RunStates.CompletedWithLimitations);
        var recorded = Record("Adiar a release", "Adiar a release 1.0 em uma semana.", "Três itens bloqueados.", runs: ["run-1", "run-2"]);
        var id = recorded["result"]!["decision"]!["id"]!.GetValue<string>();

        Golden("decision-record.json", recorded["result"]!, id);
        Golden("decision-list.json", Call("decision.list", new JsonObject { ["projectId"] = ProjectId })["result"]!, id);
        Golden("decision-get.json", Call("decision.get", new JsonObject { ["projectId"] = ProjectId, ["decisionId"] = id })["result"]!, id);
    }

    private void Golden(string file, JsonNode result, string id)
    {
        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(id, "dec-000000000001").Replace(ProjectId, "p-000000000001").ReplaceLineEndings("\n");
        text = Regex.Replace(text, "\"recordedAt\": \"[^\"]*\"", "\"recordedAt\": \"2026-11-12T12:00:00+00:00\"");
        text = Regex.Replace(text, "\"startedAt\": \"[^\"]*\"", "\"startedAt\": \"2026-11-12T10:00:00+00:00\"");

        var path = ContractFiles.Path_("fixtures", "management", file);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"{file}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
