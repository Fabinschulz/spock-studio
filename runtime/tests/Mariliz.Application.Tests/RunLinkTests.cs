using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public class RunLinkTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private readonly IntegrationHarness _harness = new();
    private readonly FileRunStore _store;
    private readonly EngineSession _session;
    private ProjectIntegration? _github;

    public RunLinkTests()
    {
        _store = new FileRunStore(_harness.DataDir);
        var tracker = new RunTracker();
        var links = new RunLinkService(_store, _store, _harness.Hub, _harness.Workspace, _harness.Clock);
        _session = new EngineSession("test", workspace: _harness.Workspace, tracker: tracker, integrations: _harness.Hub,
            runs: new RunQueries(tracker, _store, new RunCatalog(_store, _harness.Workspace, tracker, links)));
        Save("run-1");
    }

    public void Dispose() => _harness.Dispose();

    private string ProjectId => _harness.ProjectId;

    private void Save(string runId, string? projectId = null, int minutes = 0, string kind = RunKinds.Apply, string state = RunStates.Completed) =>
        _store.SaveJournal(new RunJournal
        {
            RunId = runId, ProjectId = projectId ?? ProjectId, Kind = kind, State = state, StartedAt = Now.AddMinutes(minutes)
        });

    private static WorkItem Item(string kind, string id, string title, string status = "open", string category = StatusCategories.InProgress) =>
        new(kind, id, title, "Issue", status, category, [], [], Now.AddDays(-5), Now, null, null, null, "https://example.test/" + id, []);

    // Itens escritos direto no disco, como nos outros testes.
    private async Task<ProjectIntegration> SeedGitHubAsync(params WorkItem[] items)
    {
        _github = await _harness.LinkGitHubAsync();
        SeedData(_github, items);
        return _github;
    }

    private void SeedData(ProjectIntegration integration, params WorkItem[] items)
    {
        var data = new SyncedData();
        foreach (var item in items) data.Upsert(item, Now);
        new FileSyncedDataStore(_harness.DataDir).Write(ProjectId, integration.Id, data);
    }

    private JsonObject Call(string method, JsonObject parameters) =>
        _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    private JsonObject Link(string externalId, string runId = "run-1", string? integrationId = null, string? projectId = null) =>
        Call("run.link", new JsonObject
        {
            ["projectId"] = projectId ?? ProjectId, ["runId"] = runId, ["externalId"] = externalId, ["integrationId"] = integrationId
        });

    private static string Code(JsonObject response) => response["error"]!["code"]!.GetValue<string>();

    private static JsonArray LinksOf(JsonObject response) => response["result"]!["links"]!.AsArray();

    [Fact]
    public async Task Liga_pelo_numero_com_ou_sem_cerquilha_e_traz_titulo_e_situacao_dos_dados_sincronizados()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix", "open"));

        var plain = LinksOf(Link("41"));
        var hashed = LinksOf(Link("#41"));

        Assert.Single(plain);
        Assert.Single(hashed);
        Assert.Equal("41", hashed[0]!["externalId"]!.GetValue<string>());
        Assert.Equal("Pagamento por Pix", hashed[0]!["title"]!.GetValue<string>());
        Assert.Equal("open", hashed[0]!["status"]!.GetValue<string>());
        Assert.Equal("github", hashed[0]!["provider"]!.GetValue<string>());
        Assert.True(hashed[0]!["available"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Pull_request_tambem_se_liga_e_o_tipo_vem_do_item()
    {
        await SeedGitHubAsync(Item(DataKinds.PullRequests, "57", "Corrige o relatório"));

        var links = LinksOf(Link("57"));

        Assert.Equal(DataKinds.PullRequests, links[0]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_ligacao_aparece_no_detalhe_da_execucao()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix"));
        Link("41");

        var detail = Call("run.detail", new JsonObject { ["projectId"] = ProjectId, ["runId"] = "run-1" })["result"]!;

        Assert.Equal("41", detail["links"]![0]!["externalId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Item_que_nao_foi_sincronizado_nao_se_liga()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix"));

        var response = Link("999");

        Assert.Equal("LINK_ITEM_NOT_FOUND", Code(response));
        Assert.Empty(_store.LoadLinks(ProjectId, "run-1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Identificador_em_branco_e_recusado_como_parametro_invalido(string identifier)
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));

        Assert.Equal("INVALID_PARAMS", Code(Link(identifier)));
    }

    [Theory]
    [InlineData("41 42")]
    [InlineData("41; drop")]
    [InlineData("../41")]
    public async Task Identificador_fora_do_formato_e_recusado_antes_de_procurar(string identifier)
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));

        Assert.Equal("LINK_IDENTIFIER_INVALID", Code(Link(identifier)));
    }

    [Fact]
    public async Task A_chave_do_jira_casa_sem_diferenciar_maiusculas()
    {
        await _harness.ConnectJiraAsync();
        var jira = await _harness.Hub.Projects.LinkAsync(ProjectId, (await _harness.Hub.Connections.CreateAsync(
            IntegrationProviders.Jira, "Jira 2", new Dictionary<string, string> { ["site"] = FakeJira.Site, ["email"] = FakeJira.Email },
            FakeJira.Token, CancellationToken.None)).Id, "APP", [DataKinds.Issues], CancellationToken.None);
        SeedData(jira, Item(DataKinds.Issues, "APP-12", "História de login"));

        var links = LinksOf(Link("app-12"));

        Assert.Equal("APP-12", links[0]!["externalId"]!.GetValue<string>());
        Assert.Equal("jira", links[0]!["provider"]!.GetValue<string>());
    }

    [Fact]
    public async Task Mesmo_identificador_em_duas_integracoes_pede_a_integracao()
    {
        var github = await SeedGitHubAsync(Item(DataKinds.Issues, "41", "No GitHub"));
        var jira = await _harness.Hub.Projects.LinkAsync(ProjectId, (await _harness.ConnectJiraAsync()).Id, "APP", [DataKinds.Issues], CancellationToken.None);
        SeedData(jira, Item(DataKinds.Issues, "41", "No Jira"));

        Assert.Equal("LINK_ITEM_AMBIGUOUS", Code(Link("41")));

        var chosen = LinksOf(Link("41", integrationId: jira.Id));
        Assert.Equal("No Jira", chosen[0]!["title"]!.GetValue<string>());
        Assert.NotEqual(github.Id, chosen[0]!["integrationId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ligar_de_novo_o_mesmo_item_nao_duplica_nem_audita_duas_vezes()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));

        Link("41");
        Link("41");
        Link("#41");

        Assert.Single(_store.LoadLinks(ProjectId, "run-1"));
        Assert.Single(_harness.Audit.Entries, entry => entry.Action == "run.linked");
    }

    [Fact]
    public async Task Uma_execucao_liga_a_ate_dez_itens()
    {
        await SeedGitHubAsync(Enumerable.Range(1, 11).Select(n => Item(DataKinds.Issues, n.ToString(), "x" + n)).ToArray());
        for (var n = 1; n <= 10; n++) Assert.Equal(n, LinksOf(Link(n.ToString())).Count);

        Assert.Equal("LINK_LIMIT", Code(Link("11")));
        Assert.Equal(10, _store.LoadLinks(ProjectId, "run-1").Count);
    }

    [Fact]
    public async Task Desligar_tira_a_ligacao_e_desligar_o_que_nao_esta_ligado_nao_faz_nada()
    {
        var github = await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"), Item(DataKinds.Issues, "42", "y"));
        Link("41");
        Link("42");

        var left = Call("run.unlink", new JsonObject
        {
            ["projectId"] = ProjectId, ["runId"] = "run-1", ["integrationId"] = github.Id, ["kind"] = DataKinds.Issues, ["externalId"] = "41"
        });
        var again = Call("run.unlink", new JsonObject
        {
            ["projectId"] = ProjectId, ["runId"] = "run-1", ["integrationId"] = github.Id, ["kind"] = DataKinds.Issues, ["externalId"] = "41"
        });

        Assert.Equal(["42"], LinksOf(left).Select(link => link!["externalId"]!.GetValue<string>()).ToArray());
        Assert.Single(LinksOf(again));
        Assert.Single(_harness.Audit.Entries, entry => entry.Action == "run.unlinked");
    }

    [Fact]
    public async Task Ligar_nao_reescreve_o_diario_e_so_guarda_a_referencia_sem_o_conteudo()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Titulo-que-nao-pode-ser-copiado"));
        var journal = Path.Combine(_harness.DataDir, "projects", ProjectId, "runs", "run-1", "journal.json");
        var before = File.ReadAllBytes(journal);

        Link("41");

        Assert.Equal(before, File.ReadAllBytes(journal));
        var links = File.ReadAllText(Path.Combine(_harness.DataDir, "projects", ProjectId, "runs", "run-1", "links.json"));
        Assert.DoesNotContain("Titulo-que-nao-pode-ser-copiado", links);
        Assert.Contains("\"externalId\"", links);
    }

    [Fact]
    public async Task Item_que_saiu_do_recorte_continua_ligado_mas_aparece_como_indisponivel()
    {
        var github = await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));
        Link("41");

        SeedData(github);
        var detail = Call("run.detail", new JsonObject { ["projectId"] = ProjectId, ["runId"] = "run-1" })["result"]!;

        var link = detail["links"]![0]!;
        Assert.False(link["available"]!.GetValue<bool>());
        Assert.Null(link["title"]);
        Assert.Equal("41", link["externalId"]!.GetValue<string>());
    }

    [Fact]
    public async Task So_liga_execucao_de_projeto_autorizado_que_existe_nele()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));
        Save("run-outro", _harness.OtherProjectId);

        Assert.Equal("RUN_NOT_FOUND", Code(Link("41", "run-inexistente")));
        Assert.Equal("RUN_NOT_FOUND", Code(Link("41", "../run-1")));
        // A execução do outro projeto não é encontrada pelo projeto errado.
        Assert.Equal("RUN_NOT_FOUND", Code(Link("41", "run-outro")));
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Code(Link("41", projectId: "p-desconhecido01")));
    }

    [Fact]
    public async Task Item_de_outro_projeto_nao_se_liga()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));

        // O outro projeto não tem nenhuma integração: o item 41 do primeiro não existe para ele.
        Save("run-outro", _harness.OtherProjectId);
        var response = Link("41", "run-outro", projectId: _harness.OtherProjectId);

        Assert.Equal("LINK_ITEM_NOT_FOUND", Code(response));
    }

    private JsonArray Linked(string? projectId = null)
    {
        var response = Call("run.linked", new JsonObject { ["projectId"] = projectId ?? ProjectId });
        return response["result"]!["items"]!.AsArray();
    }

    [Fact]
    public async Task O_caminho_inverso_traz_cada_item_com_as_execucoes_ligadas_a_ele()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix", "open"), Item(DataKinds.Issues, "42", "Boleto", "closed", StatusCategories.Done));
        Save("run-2", minutes: 10, kind: RunKinds.Workflow, state: RunStates.Failed);
        Save("run-3", minutes: 20);
        _harness.Clock.UtcNow = Now;
        Link("41", "run-1");
        Link("41", "run-2");
        _harness.Clock.UtcNow = Now.AddHours(1);
        Link("42", "run-3");

        var items = Linked();

        Assert.Equal(["42", "41"], items.Select(item => item!["externalId"]!.GetValue<string>()).ToArray());
        Assert.Equal("Boleto", items[0]!["title"]!.GetValue<string>());
        Assert.Equal("done", items[0]!["statusCategory"]!.GetValue<string>());
        var runs = items[1]!["runs"]!.AsArray();
        Assert.Equal(["run-2", "run-1"], runs.Select(run => run!["runId"]!.GetValue<string>()).ToArray());
        Assert.Equal("workflow", runs[0]!["kind"]!.GetValue<string>());
        Assert.Equal("failed", runs[0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task O_caminho_inverso_e_vazio_sem_ligacoes_e_nao_mistura_projetos()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));
        Assert.Empty(Linked());

        Link("41");

        Assert.Single(Linked());
        Assert.Empty(Linked(_harness.OtherProjectId));
    }

    [Fact]
    public async Task O_caminho_inverso_marca_o_item_que_saiu_do_recorte_como_indisponivel()
    {
        var github = await SeedGitHubAsync(Item(DataKinds.Issues, "41", "x"));
        Link("41");
        SeedData(github);

        var item = Linked()[0]!;

        Assert.False(item["available"]!.GetValue<bool>());
        Assert.Null(item["title"]);
        Assert.Equal("41", item["externalId"]!.GetValue<string>());
    }

    [Fact]
    public void O_caminho_inverso_de_projeto_nao_autorizado_e_recusado()
    {
        var response = Call("run.linked", new JsonObject { ["projectId"] = "p-desconhecido01" });

        Assert.Equal("PROJECT_NOT_AUTHORIZED", Code(response));
    }

    [Fact]
    public async Task Fixture_de_contrato_dos_itens_com_execucoes_ligadas()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix", "open", StatusCategories.InProgress));
        Save("run-2", minutes: 10, kind: RunKinds.Workflow, state: RunStates.Failed);
        Link("41", "run-1");
        Link("41", "run-2");

        Golden("run-linked.json", Call("run.linked", new JsonObject { ["projectId"] = ProjectId })["result"]!);
    }

    [Fact]
    public async Task Fixture_de_contrato_do_resultado_de_ligar()
    {
        await SeedGitHubAsync(Item(DataKinds.Issues, "41", "Pagamento por Pix", "open", StatusCategories.InProgress));
        var result = Link("41")["result"]!;

        Golden("run-link.json", result);
    }

    private static void Golden(string file, JsonNode result)
    {
        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        text = Regex.Replace(text, "\"integrationId\": \"[^\"]*\"", "\"integrationId\": \"int-000000000001\"");
        text = Regex.Replace(text, "\"linkedAt\": \"[^\"]*\"", "\"linkedAt\": \"2026-11-12T12:00:00.0000000+00:00\"");

        var path = ContractFiles.Path_("fixtures", "runs", file);
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
