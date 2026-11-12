using System.Text.Json.Nodes;
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
/// Assistente gerencial: o que vai para o provedor de modelo e como a resposta é conferida. O provedor é um dublê que
/// devolve um texto escrito no teste, com afirmações boas e ruins de propósito.
/// </summary>
public class ManagementAssistantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Key = "sk-chave-de-teste-assistente-0001";

    private sealed class Settings(bool configured) : IModelSettings
    {
        public ModelStatus Status() => new(configured, configured ? "openai" : "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = configured ? Key : "";
            return configured;
        }
    }

    private sealed class ScriptedChat : IModelChatTransport
    {
        public string Reply { get; set; } = "{}";
        public ChatRequest? Seen { get; private set; }
        public string SeenKey { get; private set; } = "";

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen = request;
            SeenKey = key;
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
        public ManagementAssistant Assistant { get; }
        public FileRunStore Store { get; }
        public EngineSession Session { get; }
        public ProjectIntegration Integration { get; private set; } = null!;

        public Setup(bool configured = true)
        {
            Harness.Clock.UtcNow = Now;
            var management = new ManagementService(Harness.Workspace, Harness.Hub, Harness.Clock);
            Store = new FileRunStore(Harness.DataDir);
            Assistant = new ManagementAssistant(management, Harness.Hub, Harness.Workspace,
                new ModelGateway(new Settings(configured), new NoTransport(), chat: Chat), Harness.Clock, Store);
            Session = new EngineSession("test", integrations: Harness.Hub, management: management, assistant: Assistant);
        }

        public async Task SeedAsync()
        {
            Integration = await Harness.LinkGitHubAsync();
            var data = new SyncedData();
            for (var week = 0; week < 8; week++)
                for (var n = 0; n < 2; n++)
                {
                    var closed = Now.AddDays(-7 * (8 - week) + 3 + n * 0.1);
                    data.Upsert(Item($"d{week}{n}", StatusCategories.Done, closed.AddDays(-2), closed), Now);
                }
            data.Upsert(Item("41", StatusCategories.Todo, Now.AddDays(-30), due: new DateOnly(2026, 11, 1), title: "Pagamento por Pix"), Now);
            data.Upsert(Item("42", StatusCategories.InProgress, Now.AddDays(-5)), Now);
            data.Record(new SyncExecution("sync-0000000000aa", Now, Now, SyncStatus.Succeeded, false, 1, data.Items.Count, data.Items.Count, 0, 0, null));
            new FileSyncedDataStore(Harness.DataDir).Write(Harness.ProjectId, Integration.Id, data);
        }

        public JsonObject Request(string method, JsonObject parameters) =>
            Session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

        public void Dispose() => Harness.Dispose();
    }

    private static WorkItem Item(string id, string category, DateTimeOffset created, DateTimeOffset? closed = null, DateOnly? due = null, string? title = null) =>
        new(DataKinds.Issues, id, title ?? "Item " + id, "Issue", category == StatusCategories.Done ? "completed" : "open", category, [], [],
            created, closed ?? created, closed, due, null, "https://example.test/" + id, []);

    // ------------------------------------------------------------------ Pacote de evidências

    [Fact]
    public async Task Pacote_traz_os_numeros_calculados_e_so_inclui_titulos_quando_todas_as_fontes_autorizam()
    {
        using var s = new Setup();
        await s.SeedAsync();

        var closed = s.Assistant.Evidence(s.Harness.ProjectId);
        s.Harness.Hub.Projects.Configure(s.Harness.ProjectId, s.Integration.Id, null, feedsKnowledge: true);
        var open = s.Assistant.Evidence(s.Harness.ProjectId);

        var text = string.Join("\n", closed.Facts.Select(f => $"[{f.Id}] {f.Text}"));
        Assert.Contains("[flow.issues.items] Issues: 18 itens sincronizados, 2 abertos.", text);
        Assert.Contains("[flow.issues.wip] Issues em andamento agora: 1.", text);
        Assert.Contains("[risk.overdue]", text);
        Assert.Contains("[forecast.issues]", text);
        Assert.Contains("[missing.cycleTime]", text);
        Assert.DoesNotContain("Pagamento por Pix", text);
        Assert.Contains("Itens: 41.", closed.Facts.Single(f => f.Id == "risk.overdue").Text);
        Assert.Contains("Títulos de itens não foram incluídos", string.Join(" ", closed.Withheld));
        Assert.Contains("41 (Pagamento por Pix)", open.Facts.Single(f => f.Id == "risk.overdue").Text);
        Assert.DoesNotContain(s.Harness.Project.Root, text);
        Assert.DoesNotContain(FakeGitHub.Token, text);
    }

    // ------------------------------------------------------------------ Pergunta e verificação

    [Fact]
    public async Task Resposta_passa_pela_verificacao_que_descarta_fato_inexistente_e_numero_inventado()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = new JsonObject
        {
            ["summary"] = new JsonObject { ["text"] = "Há 1 item vencido e o ritmo é de 2 entregas por semana.", ["evidence"] = new JsonArray("flow.issues.overdue", "flow.issues.throughput") },
            ["observed"] = new JsonArray(
                new JsonObject { ["text"] = "Há 1 item com prazo vencido.", ["evidence"] = new JsonArray("flow.issues.overdue") },
                new JsonObject { ["text"] = "A equipe tem 7 pessoas.", ["evidence"] = new JsonArray("team.size") }),
            ["calculations"] = new JsonArray(
                new JsonObject { ["text"] = "A média é de 2 entregas por semana.", ["evidence"] = new JsonArray("flow.issues.throughput") },
                new JsonObject { ["text"] = "A média é de 3,5 entregas por semana.", ["evidence"] = new JsonArray("flow.issues.throughput") }),
            ["inferences"] = new JsonArray(
                new JsonObject { ["text"] = "O atraso pode indicar falta de prioridade.", ["evidence"] = new JsonArray("risk.overdue") }),
            ["recommendations"] = new JsonArray(
                new JsonObject { ["text"] = "Rever o prazo do item vencido.", ["evidence"] = new JsonArray("risk.overdue") }),
            ["unavailable"] = new JsonArray(new JsonObject { ["text"] = "Não há dados de capacidade da equipe." })
        }.ToJsonString();

        var answer = await s.Assistant.AskAsync(s.Harness.ProjectId, "  Quais entregas estão comprometidas?  ", CancellationToken.None);

        Assert.Equal("Há 1 item vencido e o ritmo é de 2 entregas por semana.", answer.Summary);
        Assert.Equal(["Há 1 item com prazo vencido."], answer.Observed.Select(x => x.Text));
        Assert.Equal(["A média é de 2 entregas por semana."], answer.Calculations.Select(x => x.Text));
        Assert.Single(answer.Inferences);
        Assert.Single(answer.Recommendations);
        Assert.Equal(["Não há dados de capacidade da equipe."], answer.Unavailable);
        Assert.Equal(
            [("Dados observados", "Não cita nenhum fato do pacote de evidências."), ("Cálculos", "Traz o número 3.5, que não está nos fatos citados.")],
            answer.Rejected.Select(r => (r.Section, r.Reason)));
        Assert.Equal(["flow.issues.overdue", "flow.issues.throughput", "risk.overdue"], answer.Evidence.Select(f => f.Id).Order());

        Assert.True(s.Chat.Seen!.JsonObject);
        var sent = string.Join("\n", s.Chat.Seen.Messages.Select(m => m.Content));
        Assert.Contains("Pergunta: Quais entregas estão comprometidas?", sent);
        Assert.Contains("[flow.issues.throughput]", sent);
        Assert.DoesNotContain(Key, sent);
        Assert.Equal(Key, s.Chat.SeenKey);

        var audit = Assert.Single(s.Harness.Audit.Entries, e => e.Action == "management.assistant_answered");
        Assert.DoesNotContain("comprometidas", audit.Detail);
        Assert.Contains("\"rejected\":2", audit.Detail);
    }

    [Fact]
    public async Task Resumo_com_numero_sem_fato_citado_e_descartado()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = "{\"summary\":\"O projeto termina em 3 meses.\",\"observed\":[],\"calculations\":[],\"inferences\":[],\"recommendations\":[],\"unavailable\":[]}";

        var answer = await s.Assistant.AskAsync(s.Harness.ProjectId, "Quando termina?", CancellationToken.None);

        Assert.Equal("", answer.Summary);
        Assert.Equal("Resumo", Assert.Single(answer.Rejected).Section);
    }

    [Theory]
    [InlineData("A média caiu para 1,5 por semana.", "Média 1.5 por semana.", null)]
    [InlineData("Termina até 15/12/2026.", "provável em 5 semanas, até 2026-12-15.", null)]
    [InlineData("Faltam 05 itens.", "5 itens abertos.", null)]
    [InlineData("Faltam 12 itens.", "5 itens abertos.", "12")]
    public void Numeros_sao_conferidos_sem_depender_de_formato(string text, string fact, string? unsupported) =>
        Assert.Equal(unsupported, AnswerVerifier.UnsupportedNumber(text, [fact]));

    [Fact]
    public async Task Erros_dizem_o_motivo_sem_vazar_a_chave()
    {
        using var s = new Setup();
        await s.SeedAsync();
        using var unconfigured = new Setup(configured: false);
        s.Chat.Reply = "isto não é json";

        var invalid = s.Request("management.ask", new JsonObject { ["projectId"] = s.Harness.ProjectId, ["question"] = "Qual o risco?" });
        var shortQuestion = s.Request("management.ask", new JsonObject { ["projectId"] = s.Harness.ProjectId, ["question"] = "?" });
        var noModel = unconfigured.Request("management.ask", new JsonObject { ["projectId"] = unconfigured.Harness.ProjectId, ["question"] = "Qual o risco?" });
        var denied = s.Request("management.evidence", new JsonObject { ["projectId"] = "p-000000000000" });

        Assert.Equal("MODEL_INVALID_RESPONSE", invalid["error"]!["code"]!.GetValue<string>());
        Assert.Equal("INVALID_PARAMS", shortQuestion["error"]!["code"]!.GetValue<string>());
        Assert.Equal("MODEL_NOT_CONFIGURED", noModel["error"]!["code"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", denied["error"]!["code"]!.GetValue<string>());
        Assert.DoesNotContain(Key, invalid.ToJsonString() + noModel.ToJsonString());
    }

    // ------------------------------------------------------------------ Contrato com a interface

    [Fact]
    public async Task Respostas_do_assistente_conferem_com_as_fixtures_do_contrato()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = new JsonObject
        {
            ["summary"] = new JsonObject { ["text"] = "Há 1 item vencido e a média é de 2 entregas por semana.", ["evidence"] = new JsonArray("flow.issues.overdue", "flow.issues.throughput") },
            ["observed"] = new JsonArray(new JsonObject { ["text"] = "Há 1 item com prazo vencido.", ["evidence"] = new JsonArray("flow.issues.overdue") }),
            ["calculations"] = new JsonArray(new JsonObject { ["text"] = "A média é de 2 entregas por semana.", ["evidence"] = new JsonArray("flow.issues.throughput") }),
            ["inferences"] = new JsonArray(new JsonObject { ["text"] = "O atraso pode indicar falta de prioridade.", ["evidence"] = new JsonArray("risk.overdue") }),
            ["recommendations"] = new JsonArray(new JsonObject { ["text"] = "Rever o prazo do item vencido.", ["evidence"] = new JsonArray("risk.overdue") }),
            ["unavailable"] = new JsonArray(new JsonObject { ["text"] = "Não há dados de capacidade da equipe." })
        }.ToJsonString();

        var scenarios = new Dictionary<string, JsonNode>
        {
            ["evidence"] = s.Request("management.evidence", new JsonObject { ["projectId"] = s.Harness.ProjectId })["result"]!,
            ["answer"] = s.Request("management.ask", new JsonObject { ["projectId"] = s.Harness.ProjectId, ["question"] = "Qual o risco?" })["result"]!
        };

        foreach (var (name, result) in scenarios)
        {
            var text = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }).Replace(s.Harness.ProjectId, "p-000000000001").Replace(Path.GetFileName(s.Harness.Project.Root), "loja").ReplaceLineEndings("\n");

            var path = ContractFiles.Path_("fixtures", "management", "assistant-" + name + ".json");
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

    // ------------------------------------------------------------------ Registro na central de execuções

    private const string ValidReply =
        "{\"summary\":{\"text\":\"Há 1 item vencido.\",\"evidence\":[\"flow.issues.overdue\"]},\"observed\":[{\"text\":\"Há 1 item com prazo vencido.\",\"evidence\":[\"flow.issues.overdue\"]}],"
        + "\"calculations\":[],\"inferences\":[],\"recommendations\":[],\"unavailable\":[]}";

    private static RunJournal OnlyJournal(Setup s) => Assert.Single(s.Store.ListJournals(s.Harness.ProjectId));

    [Fact]
    public async Task Cada_pergunta_respondida_vira_uma_execucao_do_assistente_so_com_contagens()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = ValidReply;

        await s.Assistant.AskAsync(s.Harness.ProjectId, "Quais entregas estão comprometidas?", CancellationToken.None);

        var journal = OnlyJournal(s);
        Assert.Equal(RunKinds.Assistant, journal.Kind);
        Assert.Equal(RunStates.Completed, journal.State);
        Assert.StartsWith("ask-", journal.RunId);
        Assert.Equal(1, journal.ModelUsage!.Calls);
        Assert.Equal(900, journal.ModelUsage.InputTokens);
        Assert.Equal(300, journal.ModelUsage.OutputTokens);
        Assert.Equal(["answer", "verify"], journal.Steps.Select(step => step.Id));
        Assert.Contains("1 afirmações aceitas, 0 descartadas, 1 fatos citados", journal.Steps[1].Detail);
        Assert.NotNull(journal.FinishedAt);
        Assert.False(journal.ChangesApplied);
    }

    [Fact]
    public async Task O_diario_da_pergunta_nunca_guarda_o_texto_da_pergunta_nem_o_da_resposta()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = ValidReply;

        await s.Assistant.AskAsync(s.Harness.ProjectId, "Quais entregas estão comprometidas?", CancellationToken.None);

        var journalText = File.ReadAllText(Path.Combine(s.Harness.DataDir, "projects", s.Harness.ProjectId, "runs", OnlyJournal(s).RunId, "journal.json"));
        Assert.DoesNotContain("comprometidas", journalText);
        Assert.DoesNotContain("prazo vencido", journalText);
        Assert.DoesNotContain(Key, journalText);
    }

    [Fact]
    public async Task Afirmacao_descartada_pela_conferencia_deixa_a_execucao_concluida_com_limitacoes()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = "{\"summary\":\"O projeto termina em 3 meses.\",\"observed\":[],\"calculations\":[],\"inferences\":[],\"recommendations\":[],\"unavailable\":[]}";

        await s.Assistant.AskAsync(s.Harness.ProjectId, "Quando termina?", CancellationToken.None);

        var journal = OnlyJournal(s);
        Assert.Equal(RunStates.CompletedWithLimitations, journal.State);
        Assert.Single(journal.Pending);
    }

    [Fact]
    public async Task Pergunta_que_o_modelo_nao_responde_vira_execucao_que_falhou_com_o_codigo()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = "isto não é json";

        await Assert.ThrowsAsync<EngineException>(() => s.Assistant.AskAsync(s.Harness.ProjectId, "Qual o risco?", CancellationToken.None));

        var journal = OnlyJournal(s);
        Assert.Equal(RunStates.Failed, journal.State);
        Assert.Equal("MODEL_INVALID_RESPONSE", journal.Error);
        Assert.Null(journal.ModelUsage);
    }

    [Fact]
    public async Task Sem_modelo_configurado_a_falha_tambem_fica_registrada()
    {
        using var s = new Setup(configured: false);
        await s.SeedAsync();

        await Assert.ThrowsAsync<EngineException>(() => s.Assistant.AskAsync(s.Harness.ProjectId, "Qual o risco?", CancellationToken.None));

        Assert.Equal("MODEL_NOT_CONFIGURED", OnlyJournal(s).Error);
    }

    [Fact]
    public async Task Pergunta_invalida_e_projeto_nao_autorizado_nao_viram_execucao()
    {
        using var s = new Setup();
        await s.SeedAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => s.Assistant.AskAsync(s.Harness.ProjectId, "?", CancellationToken.None));
        await Assert.ThrowsAsync<EngineException>(() => s.Assistant.AskAsync("p-desconhecido01", "Qual o risco?", CancellationToken.None));

        Assert.Empty(s.Store.ListJournals(s.Harness.ProjectId));
        Assert.False(Directory.Exists(Path.Combine(s.Harness.DataDir, "projects", "p-desconhecido01")));
    }

    [Fact]
    public async Task A_pergunta_aparece_na_lista_de_execucoes_e_nao_bloqueia_uma_execucao_andando_no_projeto()
    {
        using var s = new Setup();
        await s.SeedAsync();
        s.Chat.Reply = ValidReply;
        var tracker = new RunTracker();
        Assert.True(tracker.TryStart(s.Harness.ProjectId, RunKinds.Apply, out _));

        await s.Assistant.AskAsync(s.Harness.ProjectId, "Qual o risco?", CancellationToken.None);

        var listed = new RunCatalog(s.Store, s.Harness.Workspace, tracker).List(s.Harness.ProjectId, [RunKinds.Assistant], [], null, null);
        var run = Assert.Single(listed["runs"]!.AsArray())!;
        Assert.Equal("assistant", run["kind"]!.GetValue<string>());
        Assert.Equal("completed", run["state"]!.GetValue<string>());
        Assert.Equal(1200, run["tokens"]!.GetValue<int>());
    }
}
