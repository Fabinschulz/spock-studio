using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

/// <summary>
/// O assistente sobre o portfólio: cada projeto entra com o pacote que o assistente dele montaria (a política de cada um vale por
/// inteiro), a pessoa escolhe quais entram, e a resposta passa pelo mesmo conferente. O provedor é um dublê com respostas escritas
/// no teste, com afirmações boas e ruins de propósito.
/// </summary>
public class PortfolioAssistantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Question = "Qual projeto está mais atrasado? SEGREDO-DA-PERGUNTA";

    private sealed class Settings(bool configured) : IModelSettings
    {
        public ModelStatus Status() => new(configured, configured ? "openai" : "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = configured ? "sk-chave-de-teste-portfolio-0001" : "";
            return configured;
        }
    }

    private sealed class ScriptedChat : IModelChatTransport
    {
        public string Reply { get; set; } = "{}";
        public ChatRequest? Seen { get; private set; }

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen = request;
            return Task.FromResult(new ChatTransportResult(Reply, "gpt-4o-mini-2024-07-18", 900, 300));
        }
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException();
    }

    private sealed class Setup : IDisposable
    {
        public IntegrationHarness Harness { get; } = new();
        public ScriptedChat Chat { get; } = new();
        public PortfolioAssistant Portfolio { get; }
        public FileRunStore Store { get; }
        public EngineSession Session { get; }
        public ProjectIntegration IntegrationA { get; private set; } = null!;
        /// <summary>Os projetos na ordem em que o portfólio os lista (p1 e p2): o nome da pasta temporária é sorteado, a ordem é que decide quem é quem.</summary>
        public string A { get; private set; } = "";
        public string B { get; private set; } = "";
        public ProjectIntegration IntegrationB { get; private set; } = null!;

        public Setup(bool configured = true, int? factsPerProject = null)
        {
            Harness.Clock.UtcNow = Now;
            var management = new ManagementService(Harness.Workspace, Harness.Hub, Harness.Clock);
            Store = new FileRunStore(Harness.DataDir);
            var gateway = new ModelGateway(new Settings(configured), new NoTransport(), chat: Chat);
            var assistant = new ManagementAssistant(management, Harness.Hub, Harness.Workspace, gateway, Harness.Clock, Store);
            Portfolio = new PortfolioAssistant(assistant, Harness.Workspace, gateway, Harness.Hub, Harness.Clock)
            {
                FactsPerProject = factsPerProject ?? PortfolioAssistant.MaxFactsPerProject
            };
            Session = new EngineSession("test", integrations: Harness.Hub, management: management, assistant: assistant, portfolioAssistant: Portfolio);
        }

        /// <summary>
        /// O projeto A tem 18 itens (16 entregues, um vencido com título e um em andamento) e o B tem 3 itens entregues e um aberto
        /// vencido com outro título. Cada um com a sua fonte e a sua política (nascem fechadas).
        /// </summary>
        public async Task SeedAsync()
        {
            var ordered = Harness.Workspace.Projects();
            A = ordered[0].ProjectId;
            B = ordered[1].ProjectId;
            var first = await Harness.ConnectGitHubAsync("GitHub A");
            IntegrationA = await Harness.Hub.Projects.LinkAsync(A, first.Id, FakeGitHub.Repository,
                [DataKinds.Issues, DataKinds.PullRequests], CancellationToken.None);
            var second = await Harness.ConnectGitHubAsync("GitHub B");
            IntegrationB = await Harness.Hub.Projects.LinkAsync(B, second.Id, FakeGitHub.Repository,
                [DataKinds.Issues, DataKinds.PullRequests], CancellationToken.None);

            var a = new SyncedData();
            for (var week = 0; week < 8; week++)
                for (var n = 0; n < 2; n++)
                {
                    var closed = Now.AddDays(-7 * (8 - week) + 3 + n * 0.1);
                    a.Upsert(Item($"d{week}{n}", StatusCategories.Done, closed.AddDays(-2), closed), Now);
                }
            a.Upsert(Item("41", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1), title: "Pagamento por Pix"), Now);
            a.Upsert(Item("42", StatusCategories.InProgress, Now.AddDays(-5)), Now);
            a.Record(new SyncExecution("sync-0000000000aa", Now, Now, SyncStatus.Succeeded, false, 1, a.Items.Count, a.Items.Count, 0, 0, null));
            new FileSyncedDataStore(Harness.DataDir).Write(A, IntegrationA.Id, a);

            var b = new SyncedData();
            for (var n = 0; n < 3; n++)
                b.Upsert(Item($"b{n}", StatusCategories.Done, Now.AddDays(-20 + n), Now.AddDays(-10 + n)), Now);
            b.Upsert(Item("77", StatusCategories.Todo, Now.AddDays(-40), due: new DateOnly(2026, 10, 20), title: "Titulo secreto do projeto B"), Now);
            b.Record(new SyncExecution("sync-0000000000bb", Now, Now, SyncStatus.Succeeded, false, 1, b.Items.Count, b.Items.Count, 0, 0, null));
            new FileSyncedDataStore(Harness.DataDir).Write(B, IntegrationB.Id, b);
        }

        public JsonObject Request(string method, JsonObject parameters) =>
            Session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

        public void Dispose() => Harness.Dispose();
    }

    private static WorkItem Item(string id, string category, DateTimeOffset created, DateTimeOffset? closed = null, DateOnly? due = null, string? title = null) =>
        new(DataKinds.Issues, id, title ?? "Item " + id, "Issue", category == StatusCategories.Done ? "completed" : "open", category, [], [],
            created, closed ?? created, closed, due, null, "https://example.test/" + id, []);

    private static string Reply(string summary, params (string Text, string[] Evidence)[] observed) => new JsonObject
    {
        ["summary"] = new JsonObject { ["text"] = summary, ["evidence"] = new JsonArray() },
        ["observed"] = new JsonArray(observed.Select(item => (JsonNode)new JsonObject
        {
            ["text"] = item.Text,
            ["evidence"] = new JsonArray(item.Evidence.Select(id => (JsonNode)id).ToArray())
        }).ToArray()),
        ["calculations"] = new JsonArray(),
        ["inferences"] = new JsonArray(),
        ["recommendations"] = new JsonArray(),
        ["unavailable"] = new JsonArray(new JsonObject { ["text"] = "Os fatos não dizem a causa." })
    }.ToJsonString();

    private static string AliasOf(PortfolioEvidence evidence, string projectId) => evidence.Projects.Single(project => project.ProjectId == projectId).Alias;

    // ------------------------------------------------------------------ O pacote

    [Fact]
    public async Task Cada_projeto_entra_com_os_proprios_fatos_prefixados_pelo_alias_e_a_data_de_hoje_entra_uma_vez()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var evidence = s.Portfolio.Evidence(null);

        Assert.Equal(["p1", "p2"], evidence.Projects.Select(project => project.Alias));
        Assert.Equal(1, evidence.Facts.Count(fact => fact.Id == "today"));
        var a = AliasOf(evidence, s.A);
        var b = AliasOf(evidence, s.B);
        Assert.Equal(("p1", "p2"), (a, b));
        var facts = evidence.Facts.ToDictionary(fact => fact.Id);
        Assert.Equal("Issues: 18 itens sincronizados, 2 abertos.", facts[$"{a}.flow.issues.items"].Text);
        Assert.Equal("Issues: 4 itens sincronizados, 1 abertos.", facts[$"{b}.flow.issues.items"].Text);
        Assert.StartsWith($"Projeto {a}: ", facts[$"{a}.name"].Text);
        Assert.Equal(evidence.Projects.Sum(project => project.Facts) + 1, evidence.Facts.Count);
        Assert.Equal(evidence.Facts.Count, evidence.Facts.Select(fact => fact.Id).Distinct().Count());
    }

    [Fact]
    public async Task Fato_cortado_pelo_limite_por_projeto_aparece_nos_avisos_em_vez_de_sumir_em_silencio()
    {
        using var s = new Setup(factsPerProject: 5);
        await s.SeedAsync();

        var evidence = s.Portfolio.Evidence(null);

        Assert.All(evidence.Projects, project => Assert.Equal(5, project.Facts));
        foreach (var project in evidence.Projects)
        {
            var line = Assert.Single(evidence.Withheld, item => item.StartsWith(project.Name + ": ", StringComparison.Ordinal) && item.Contains("ficaram de fora por passarem do limite de 5", StringComparison.Ordinal));
            Assert.Matches(@": \d+ fatos ficaram de fora", line);
        }
    }

    [Fact]
    public async Task Dentro_do_limite_por_projeto_nenhum_aviso_de_corte_aparece()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var evidence = s.Portfolio.Evidence(null);

        Assert.DoesNotContain(evidence.Withheld, line => line.Contains("passarem do limite", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_politica_de_cada_projeto_vale_por_inteiro_titulo_so_no_que_autorizou()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Harness.Hub.Projects.Configure(s.A, s.IntegrationA.Id, null, feedsKnowledge: true);

        var evidence = s.Portfolio.Evidence(null);

        var text = string.Join("\n", evidence.Facts.Select(fact => fact.Text));
        Assert.Contains("Pagamento por Pix", text);
        Assert.DoesNotContain("Titulo secreto do projeto B", text);
        // Só o projeto B fechou a política: o aviso é dele e leva o nome dele.
        var closedLine = Assert.Single(evidence.Withheld, line => line.Contains("Títulos de itens não foram incluídos", StringComparison.Ordinal));
        Assert.StartsWith(evidence.Projects.Single(project => project.ProjectId == s.B).Name + ": ", closedLine);
        Assert.Contains(evidence.Withheld, line => line.StartsWith(evidence.Projects.Single(project => project.ProjectId == s.A).Name + ": Títulos de itens citados em riscos foram incluídos", StringComparison.Ordinal));
        Assert.Contains("Nenhum código, documento ou arquivo de nenhum projeto é enviado.", evidence.Withheld);
    }

    [Fact]
    public async Task A_pessoa_escolhe_os_projetos_e_um_que_nao_esta_aberto_e_recusado()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var only = s.Portfolio.Evidence([s.B]);
        var unknown = Assert.Throws<EngineException>(() => s.Portfolio.Evidence(["p-ffffffffffff"]));
        var tooMany = Assert.ThrowsAny<Exception>(() => s.Portfolio.Evidence(Enumerable.Range(0, 11).Select(index => $"p-{index:x12}").ToList()));

        var project = Assert.Single(only.Projects);
        Assert.Equal((s.B, "p1"), (project.ProjectId, project.Alias));
        Assert.DoesNotContain(only.Facts, fact => fact.Text.Contains("18 itens sincronizados", StringComparison.Ordinal));
        Assert.Equal("PROJECT_NOT_AUTHORIZED", unknown.Code);
        Assert.NotNull(tooMany);
    }

    [Fact]
    public async Task O_nome_do_projeto_entra_sem_quebra_de_linha_nem_caractere_de_controle()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var evidence = s.Portfolio.Evidence(null);

        Assert.All(evidence.Projects, project => Assert.DoesNotContain(project.Name, ch => char.IsControl(ch)));
        Assert.All(evidence.Facts.Where(fact => fact.Id.EndsWith(".name", StringComparison.Ordinal)), fact => Assert.DoesNotContain('\n', fact.Text));
    }

    // ------------------------------------------------------------------ A pergunta

    [Fact]
    public async Task O_provedor_recebe_os_fatos_separados_por_projeto_e_a_pergunta()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.");

        await s.Portfolio.AskAsync(Question, null, CancellationToken.None);

        var user = s.Chat.Seen!.Messages.Single(message => message.Role == "user").Content;
        Assert.Contains("Projeto p1:", user);
        Assert.Contains("Projeto p2:", user);
        Assert.Contains("[today]", user);
        Assert.Contains("Pergunta: " + Question, user);
        var system = s.Chat.Seen.Messages.Single(message => message.Role == "system").Content;
        Assert.Contains("não some, não tire média e não calcule diferença", system);
    }

    [Fact]
    public async Task A_resposta_boa_passa_com_as_citacoes_e_a_que_soma_entre_projetos_e_descartada()
    {
        using var s = new Setup();
        await s.SeedAsync();
        var evidence = s.Portfolio.Evidence(null);
        var a = AliasOf(evidence, s.A);
        var b = AliasOf(evidence, s.B);
        s.Chat.Reply = Reply(
            "Sem resumo numérico.",
            ("Issues: 18 itens sincronizados, 2 abertos.", [$"{a}.flow.issues.items"]),
            ("Issues: 4 itens sincronizados, 1 abertos.", [$"{b}.flow.issues.items"]),
            ("Os dois projetos somam 22 itens.", [$"{a}.flow.issues.items", $"{b}.flow.issues.items"]),
            ("Sem fato citado.", []));

        var result = await s.Portfolio.AskAsync(Question, null, CancellationToken.None);

        Assert.Equal(2, result.Answer.Observed.Count);
        Assert.Equal([$"{a}.flow.issues.items"], result.Answer.Observed[0].Evidence);
        Assert.Equal(2, result.Answer.Rejected.Count);
        Assert.Contains(result.Answer.Rejected, item => item.Reason.Contains("22", StringComparison.Ordinal));
        Assert.Contains(result.Answer.Rejected, item => item.Reason.Contains("nenhum fato", StringComparison.Ordinal));
        Assert.Equal("portfolio", result.Answer.ProjectId);
        Assert.Equal(["Os fatos não dizem a causa."], result.Answer.Unavailable);
        Assert.Equal(2, result.Projects.Count);
        Assert.Equal(900, result.Answer.InputTokens);
    }

    [Fact]
    public async Task Pergunta_curta_ou_longa_demais_e_recusada_antes_de_chamar_o_provedor()
    {
        using var s = new Setup();
        await s.SeedAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => s.Portfolio.AskAsync("oi", null, CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => s.Portfolio.AskAsync(new string('x', 501), null, CancellationToken.None));

        Assert.Null(s.Chat.Seen);
    }

    [Fact]
    public async Task Sem_modelo_configurado_a_pergunta_falha_com_codigo_e_cada_projeto_ganha_o_diario_da_falha()
    {
        using var s = new Setup(configured: false);
        await s.SeedAsync();

        var ex = await Assert.ThrowsAsync<EngineException>(() => s.Portfolio.AskAsync(Question, null, CancellationToken.None));

        Assert.Equal("MODEL_NOT_CONFIGURED", ex.Code);
        foreach (var projectId in new[] { s.A, s.B })
        {
            var journal = Assert.Single(s.Store.ListJournals(projectId));
            Assert.Equal((RunKinds.Assistant, RunStates.Failed, "MODEL_NOT_CONFIGURED"), (journal.Kind, journal.State, journal.Error));
        }
    }

    [Fact]
    public async Task O_diario_de_cada_projeto_guarda_contagens_e_nunca_o_texto_da_pergunta_nem_a_resposta()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.", ("Issues: 18 itens sincronizados, 2 abertos.", ["p1.flow.issues.items"]));

        await s.Portfolio.AskAsync(Question, null, CancellationToken.None);

        foreach (var projectId in new[] { s.A, s.B })
        {
            var journal = Assert.Single(s.Store.ListJournals(projectId));
            Assert.Equal(RunKinds.Assistant, journal.Kind);
            Assert.Equal(1200, journal.ModelUsage!.InputTokens + journal.ModelUsage.OutputTokens);
            var written = System.Text.Json.JsonSerializer.Serialize(journal);
            Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", written, StringComparison.Ordinal);
            Assert.DoesNotContain("Issues: 18", written, StringComparison.Ordinal);
        }
        var audit = Assert.Single(s.Harness.Audit.Entries, item => item.Action == "management.portfolio_answered");
        Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", audit.Detail, StringComparison.Ordinal);
        Assert.Contains("\"projects\":2", audit.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_diario_de_cada_projeto_diz_que_a_pergunta_cobriu_varios_projetos_e_que_o_uso_e_o_da_pergunta_inteira()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.", ("Issues: 18 itens sincronizados, 2 abertos.", ["p1.flow.issues.items"]));

        await s.Portfolio.AskAsync(Question, null, CancellationToken.None);

        // O mesmo uso aparece nos dois diários (cada projeto mostra o custo real da pergunta que o envolveu); o passo explica isso.
        var usages = new List<int>();
        foreach (var projectId in new[] { s.A, s.B })
        {
            var journal = Assert.Single(s.Store.ListJournals(projectId));
            var step = Assert.Single(journal.Steps, item => item.Id == "portfolio");
            Assert.Equal("Pergunta ao portfólio", step.Title);
            Assert.Contains("cobriu 2 projetos", step.Detail, StringComparison.Ordinal);
            Assert.Contains("uso de modelo", step.Detail, StringComparison.Ordinal);
            Assert.Contains("pergunta inteira", step.Detail, StringComparison.Ordinal);
            usages.Add(journal.ModelUsage!.InputTokens + journal.ModelUsage.OutputTokens);
        }
        Assert.Equal(usages[0], usages[1]);
    }

    [Fact]
    public async Task Uma_pergunta_so_sobre_um_projeto_nao_ganha_o_passo_do_portfolio()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.", ("Issues: 4 itens sincronizados, 1 abertos.", ["p1.flow.issues.items"]));

        await s.Portfolio.AskAsync(Question, [s.B], CancellationToken.None);

        var journal = Assert.Single(s.Store.ListJournals(s.B));
        Assert.DoesNotContain(journal.Steps, item => item.Id == "portfolio");
    }

    // ------------------------------------------------------------------ Protocolo e contrato

    [Fact]
    public async Task Os_metodos_do_protocolo_devolvem_o_pacote_e_a_resposta_e_recusam_parametro_invalido()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.", ("Issues: 18 itens sincronizados, 2 abertos.", ["p1.flow.issues.items"]));

        var evidence = s.Request(ProtocolMethods.ManagementPortfolioEvidence, new JsonObject());
        var chosen = s.Request(ProtocolMethods.ManagementPortfolioEvidence, new JsonObject { ["projectIds"] = new JsonArray(s.B) });
        var answer = s.Request(ProtocolMethods.ManagementPortfolioAsk, new JsonObject { ["question"] = Question });
        var missing = s.Request(ProtocolMethods.ManagementPortfolioAsk, new JsonObject());
        var badList = s.Request(ProtocolMethods.ManagementPortfolioEvidence, new JsonObject { ["projectIds"] = "p-1" });

        Assert.Equal(2, evidence["result"]!["projects"]!.AsArray().Count);
        Assert.Single(chosen["result"]!["projects"]!.AsArray());
        Assert.Equal(2, answer["result"]!["projects"]!.AsArray().Count);
        Assert.Equal("portfolio", answer["result"]!["answer"]!["projectId"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, missing["error"]!["code"]!.GetValue<string>());
        Assert.Equal(ProtocolErrorCodes.InvalidParams, badList["error"]!["code"]!.GetValue<string>());
        Assert.Contains(ProtocolMethods.ManagementPortfolioAsk, ProtocolMethods.Management);
        Assert.Contains(ProtocolMethods.ManagementPortfolioEvidence, ProtocolMethods.Management);
    }

    [Fact]
    public async Task As_respostas_do_protocolo_seguem_o_contrato_que_a_interface_le()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = Reply("Sem resumo numérico.", ("Issues: 18 itens sincronizados, 2 abertos.", ["p1.flow.issues.items"]));

        Golden("portfolio-evidence.json", s.Request(ProtocolMethods.ManagementPortfolioEvidence, new JsonObject())["result"]!, s);
        Golden("portfolio-answer.json", s.Request(ProtocolMethods.ManagementPortfolioAsk, new JsonObject { ["question"] = Question })["result"]!, s);
    }

    private static void Golden(string file, JsonNode result, Setup s)
    {
        var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        text = text.Replace(s.B, "p-000000000002").Replace(s.A, "p-000000000001");
        text = Regex.Replace(text, "\"(generatedAt|answeredAt)\": \"[^\"]*\"", "\"$1\": \"2026-11-10T12:00:00+00:00\"");
        // Os nomes vêm das pastas temporárias do teste: a fixture guarda nomes estáveis, na ordem do portfólio.
        var projects = s.Harness.Workspace.Projects();
        text = text.Replace(projects[0].DisplayName, "Loja").Replace(projects[1].DisplayName, "Painel");
        Assert.DoesNotContain("SEGREDO-DA-PERGUNTA", text, StringComparison.Ordinal);

        var path = ContractFiles.Path_("fixtures", "management", file);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, text + "\n");
            return;
        }

        Assert.True(text == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"{file}: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
