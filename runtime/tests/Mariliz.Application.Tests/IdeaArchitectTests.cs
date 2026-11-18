using Mariliz.Core.Policy;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ideas;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// O modelo é um dublê com respostas escritas no teste; o que se prova aqui é o que o motor confere, aceita e grava.
public sealed class IdeaArchitectTests : IDisposable
{
    private const string Idea = "Um app para lojistas acompanharem pedidos e avisarem o cliente quando o pedido sair para entrega.";
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace = new(new LocalWorkspaceFileSystem());
    private readonly FileRunStore _store;
    private readonly RunTracker _tracker = new();
    private readonly ScriptedChat _chat = new();
    private readonly string _projectId;

    public IdeaArchitectTests()
    {
        _store = new FileRunStore(_project.DataDir);
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private sealed class Settings(bool configured) : IModelSettings
    {
        public ModelStatus Status() => new(configured, configured ? "openai" : "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = configured ? "sk-chave-de-teste-ideia-000000001" : "";
            return configured;
        }
    }

    private sealed class ScriptedChat : IModelChatTransport
    {
        public Queue<string> Replies { get; } = new();
        public List<ChatRequest> Seen { get; } = [];

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(new ChatTransportResult(Replies.Count > 0 ? Replies.Dequeue() : "{}", "gpt-4o-mini-2024-07-18", 800, 400));
        }
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException();
    }

    private IdeaArchitectService Service(bool configured = true) =>
        new(_workspace, new ModelGateway(new Settings(configured), new NoTransport(), chat: _chat), new TestFilesFactory(), _store,
            new FakeClock { UtcNow = Now }, _tracker);

    private static JsonObject Spec() => JsonNode.Parse("""
        {"title": "Pedidos da loja", "problem": "O lojista não sabe onde está cada pedido. O cliente pergunta por telefone.",
         "users": ["Lojista", "Cliente"], "goals": ["Menos ligações de clientes"],
         "requirements": [
           {"id": "REQ-01", "text": "Quando um pedido for criado, o sistema deve registrá-lo como recebido.", "priority": "must"},
           {"id": "REQ-02", "text": "Quando o pedido sair para entrega, o sistema deve avisar o cliente.", "priority": "must"},
           {"id": "REQ-03", "text": "O sistema deve mostrar ao lojista os pedidos do dia.", "priority": "could"}],
         "nonFunctional": [{"id": "NFR-01", "text": "O sistema deve responder em até dois segundos."}],
         "outOfScope": ["Pagamento"], "openQuestions": ["O aviso é por e-mail ou mensagem?"]}
        """)!.AsObject();

    private static JsonObject Architecture() => JsonNode.Parse("""
        {"style": "Monólito modular", "summary": "Uma API com um painel web e um serviço de avisos.",
         "components": [
           {"id": "CMP-01", "name": "Pedidos", "responsibility": "Guarda e muda o estado dos pedidos.", "requirements": ["REQ-01", "REQ-03"]},
           {"id": "CMP-02", "name": "Avisos", "responsibility": "Avisa o cliente.", "requirements": ["REQ-02"]}],
         "stack": [{"area": "backend", "choice": ".NET", "reason": "Equipe já conhece."}],
         "data": ["Pedido no PostgreSQL"], "integrations": ["Serviço de e-mail"],
         "risks": [{"text": "Aviso duplicado.", "mitigation": "Chave de idempotência por pedido."}],
         "decisions": [{"title": "Banco", "choice": "PostgreSQL", "reason": "Relacional e simples."}]}
        """)!.AsObject();

    private static JsonObject Backlog() => JsonNode.Parse("""
        {"epics": [{"id": "EPIC-01", "title": "Ciclo do pedido", "stories": [
          {"id": "US-01", "title": "Como lojista, quero registrar pedidos para acompanhá-los", "requirements": ["REQ-01", "REQ-03"],
           "acceptance": ["Dado um pedido novo, quando salvo, então ele aparece como recebido"], "estimate": "P"},
          {"id": "US-02", "title": "Como cliente, quero ser avisado da entrega para me organizar", "requirements": ["REQ-02"],
           "acceptance": ["Dado um pedido em entrega, quando o estado muda, então o cliente recebe o aviso"], "estimate": "M"}]}]}
        """)!.AsObject();

    private static IdeaArtifacts Approved(JsonObject? spec = null, JsonObject? architecture = null, JsonObject? backlog = null) =>
        new(Idea, spec, architecture, backlog);

    private static IdeaArtifacts All() => Approved(Spec(), Architecture(), Backlog());

    private static async Task<RunHandle> Finished(RunHandle run)
    {
        await run.Completion!;
        return run;
    }

    [Fact]
    public async Task Especificacao_valida_passa_na_primeira_e_o_diario_nao_guarda_o_texto()
    {
        _chat.Replies.Enqueue(Spec().ToJsonString());

        var draft = await Service().DraftAsync(_projectId, IdeaStages.Specification, Approved(), CancellationToken.None);

        Assert.True(draft.Valid);
        Assert.Equal(1, draft.Attempts);
        Assert.Equal((800, 400), (draft.InputTokens, draft.OutputTokens));
        Assert.All(draft.Checks, check => Assert.True(check.Passed, check.Label));
        Assert.Contains($"<ideia>\n{Idea}\n</ideia>", _chat.Seen[0].Messages[1].Content);
        Assert.True(_chat.Seen[0].JsonObject);

        var journal = Assert.Single(_store.ListJournals(_projectId));
        Assert.Equal((RunKinds.Idea, RunStates.Completed), (journal.Kind, journal.State));
        Assert.Equal(1, journal.ModelUsage!.Calls);
        Assert.DoesNotContain("lojistas", JsonSerializer.Serialize(journal));
    }

    [Fact]
    public async Task Resposta_que_nao_passa_volta_ao_modelo_com_o_que_falhou_e_a_segunda_vale()
    {
        var broken = Spec();
        broken["requirements"]![1]!["text"] = "Avisar o cliente.";
        broken["requirements"]![2]!["id"] = "REQ-01";
        _chat.Replies.Enqueue(broken.ToJsonString());
        _chat.Replies.Enqueue(Spec().ToJsonString());

        var draft = await Service().DraftAsync(_projectId, IdeaStages.Specification, Approved(), CancellationToken.None);

        Assert.True(draft.Valid);
        Assert.Equal(2, draft.Attempts);
        Assert.Equal((1600, 800), (draft.InputTokens, draft.OutputTokens));
        var retry = _chat.Seen[1].Messages[1].Content;
        Assert.Contains("não passou nestas verificações", retry);
        Assert.Contains("Cada requisito diz o que o sistema deve fazer (REQ-02)", retry);
        Assert.Contains("(REQ-01)", retry);
    }

    [Fact]
    public async Task Depois_de_duas_tentativas_devolve_o_que_falhou_sem_inventar_aprovacao()
    {
        _chat.Replies.Enqueue("não é json");
        _chat.Replies.Enqueue("""{"title": "x"}""");

        var draft = await Service().DraftAsync(_projectId, IdeaStages.Specification, Approved(), CancellationToken.None);

        Assert.False(draft.Valid);
        Assert.Equal(2, draft.Attempts);
        Assert.Contains(draft.Checks, check => check.Id == "shape" && !check.Passed);
        Assert.Equal(RunStates.CompletedWithLimitations, Assert.Single(_store.ListJournals(_projectId)).State);
    }

    [Fact]
    public async Task Arquitetura_e_backlog_so_aceitam_o_que_rastreia_ate_os_requisitos()
    {
        var architecture = Architecture();
        architecture["components"]![1]!["requirements"] = new JsonArray("REQ-99");
        var backlog = Backlog();
        backlog["epics"]![0]!["stories"]![1]!["acceptance"] = new JsonArray();
        backlog["epics"]![0]!["stories"]![1]!["requirements"] = new JsonArray("REQ-03");
        _chat.Replies.Enqueue(architecture.ToJsonString());
        _chat.Replies.Enqueue(architecture.ToJsonString());
        _chat.Replies.Enqueue(backlog.ToJsonString());
        _chat.Replies.Enqueue(backlog.ToJsonString());
        var service = Service();

        var arch = await service.DraftAsync(_projectId, IdeaStages.Architecture, Approved(Spec()), CancellationToken.None);
        var back = await service.DraftAsync(_projectId, IdeaStages.Backlog, Approved(Spec(), Architecture()), CancellationToken.None);

        Assert.Equal("REQ-99", arch.Checks.Single(check => check.Id == "traces").Detail);
        Assert.Equal("REQ-02", arch.Checks.Single(check => check.Id == "coverage").Detail);
        Assert.Equal("REQ-02", back.Checks.Single(check => check.Id == "coverage").Detail);
        Assert.Equal("US-02", back.Checks.Single(check => check.Id == "acceptance").Detail);
        Assert.Contains("<etapa-aprovada nome=\"especificação\">", _chat.Seen[2].Messages[1].Content);
        Assert.Contains("<etapa-aprovada nome=\"arquitetura\">", _chat.Seen[2].Messages[1].Content);
    }

    [Fact]
    public async Task Cada_etapa_exige_a_anterior_aprovada_e_valida()
    {
        var service = Service();
        var invalidSpec = Spec();
        invalidSpec["requirements"] = new JsonArray();

        await Assert.ThrowsAsync<ParamException>(() => service.DraftAsync(_projectId, IdeaStages.Architecture, Approved(), CancellationToken.None));
        await Assert.ThrowsAsync<ParamException>(() => service.DraftAsync(_projectId, IdeaStages.Architecture, Approved(invalidSpec), CancellationToken.None));
        await Assert.ThrowsAsync<ParamException>(() => service.DraftAsync(_projectId, IdeaStages.Backlog, Approved(Spec()), CancellationToken.None));
        await Assert.ThrowsAsync<ParamException>(() => service.DraftAsync(_projectId, IdeaStages.Specification, new IdeaArtifacts("curta", null, null, null), CancellationToken.None));
        await Assert.ThrowsAsync<ParamException>(() => service.DraftAsync(_projectId, "deploy", Approved(), CancellationToken.None));
        Assert.Empty(_chat.Seen);
    }

    [Fact]
    public async Task Sem_modelo_configurado_diz_isso_e_registra_a_falha()
    {
        var error = await Assert.ThrowsAsync<EngineException>(() =>
            Service(configured: false).DraftAsync(_projectId, IdeaStages.Specification, Approved(), CancellationToken.None));

        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
        Assert.Equal(RunStates.Failed, Assert.Single(_store.ListJournals(_projectId)).State);
    }

    [Fact]
    public async Task Grava_os_documentos_aprovados_e_a_gravacao_pode_ser_desfeita()
    {
        var service = Service();
        var plan = service.Plan(_projectId, All());
        Assert.Equal([IdeaDocuments.SpecificationFile(HarnessNaming.Spock), IdeaDocuments.ArchitectureFile(HarnessNaming.Spock), IdeaDocuments.BacklogFile(HarnessNaming.Spock)], plan.Changes.Select(change => change.Path));
        Assert.All(plan.Changes, change => Assert.Equal(OpKinds.Create, change.Op));
        Assert.Equal("STALE_PLAN", Assert.Throws<EngineException>(() => service.StartSave(_projectId, All(), "idea-outro")).Code);

        var run = await Finished(service.StartSave(_projectId, All(), plan.PlanId));

        Assert.Equal(RunStates.Completed, run.State);
        var spec = File.ReadAllText(Path.Combine(_project.Root, ".spock", "arquitetura", "especificacao.md"));
        Assert.Contains("**REQ-02** (must): Quando o pedido sair para entrega, o sistema deve avisar o cliente.", spec);
        Assert.Contains("### US-02", File.ReadAllText(Path.Combine(_project.Root, ".spock", "arquitetura", "backlog.md")));
        Assert.Contains("**CMP-02 Avisos**", File.ReadAllText(Path.Combine(_project.Root, ".spock", "arquitetura", "arquitetura.md")));

        var revert = new RunHandle("run-reverte0002", _projectId, RunKinds.Revert, (_, _, _, _, _) => { });
        var result = new RevertService(_store, new FakeClock { UtcNow = Now }).Execute(revert, new TestFilesFactory().Open(_project.Root), run.RunId, CancellationToken.None);
        Assert.True(result["verified"]!.GetValue<bool>());
        Assert.False(Directory.Exists(Path.Combine(_project.Root, ".spock", "arquitetura")));
    }

    [Fact]
    public void Gravar_exige_backlog_aprovado_e_valido()
    {
        var backlog = Backlog();
        backlog["epics"] = new JsonArray();

        Assert.Throws<ParamException>(() => Service().Plan(_projectId, Approved(Spec(), Architecture())));
        Assert.Throws<ParamException>(() => Service().Plan(_projectId, Approved(Spec(), Architecture(), backlog)));
    }

    [Theory]
    [InlineData("idea.draft", "draft.json")]
    [InlineData("idea.plan", "plan.json")]
    public void Fixture_de_contrato_da_ideia(string method, string fixture)
    {
        _chat.Replies.Enqueue(Spec().ToJsonString());
        var session = new EngineSession("test", ideas: Service());
        var parameters = new JsonObject { ["projectId"] = _projectId, ["idea"] = Idea, ["stage"] = IdeaStages.Specification };
        if (method == "idea.plan")
        {
            parameters["specification"] = Spec();
            parameters["architecture"] = Architecture();
            parameters["backlog"] = Backlog();
        }

        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters
        }.ToJsonString()).Response;
        var result = response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());
        if (result.ContainsKey("projectId")) result["projectId"] = "p-000000000001";

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "idea", fixture);
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            $"{method}: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
