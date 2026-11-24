using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ideas;
using Mariliz.Application.Mcp;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// W4-3: a escolha de stack segue o §10 do catálogo, nos dois modos. O "dataset" é a lista de arquiteturas erradas de
// propósito, cada uma caindo na verificação certa; o critério do roadmap ("para requisitos iguais, a escolha é estável; cada
// escolha cita o critério") tem casos próprios. No modo Sessão, o agente grava a jornada inteira num arquivo e o motor confere.
public sealed class ProjectDesignTests : IDisposable
{
    private const string Idea = "Um app para lojistas acompanharem pedidos e avisarem o cliente quando o pedido sair para entrega.";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;

    public ProjectDesignTests()
    {
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

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
         "factors": [
           {"id": "F1", "criterion": "10.4-conhecimento-da-equipe", "fact": "A equipe trabalha com .NET.", "favors": ".NET"},
           {"id": "F2", "criterion": "10.4-dominio-com-regras-e-transacoes", "fact": "O pedido muda de estado por regras.", "favors": ".NET"},
           {"id": "F3", "criterion": "10.1-react", "fact": "A equipe já usa React no painel de outra loja."}],
         "stack": [
           {"area": "backend", "choice": "ASP.NET Core", "reason": "Equipe já conhece.", "question": "10.4", "option": ".NET",
            "factors": ["F1", "F2"], "requirements": ["NFR-01"],
            "alternatives": [{"choice": "Node/TS", "whyNot": "A equipe não trabalha com Node."}],
            "tradeoffs": "Imagem do contêiner maior que a de um serviço em Go."},
           {"area": "frontend", "choice": "React com Vite", "reason": "Painel interno sem SEO.", "question": "10.1", "option": "React",
            "factors": ["F3"], "alternatives": [{"choice": "Angular", "whyNot": "Ninguém na equipe usa Angular."}],
            "tradeoffs": "Roteador e formulários vêm de bibliotecas, não do framework."},
           {"area": "dados", "choice": "PostgreSQL", "reason": "Relacional e simples.", "requirements": ["REQ-01"],
            "alternatives": [{"choice": "MongoDB", "whyNot": "Os pedidos têm relações e transações."}],
            "tradeoffs": "Esquema precisa de migração a cada mudança."}],
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

    private static JsonObject Design() => new()
    {
        ["idea"] = Idea,
        ["specification"] = Spec(),
        ["architecture"] = Architecture(),
        ["backlog"] = Backlog()
    };

    private static JsonObject Stack(JsonObject architecture, int index) => architecture["stack"]![index]!.AsObject();

    private static IReadOnlyList<string> Failed(JsonObject architecture) =>
        StackChecks.For(architecture, Spec()).Where(check => !check.Passed).Select(check => check.Id).ToList();

    [Fact]
    public void Os_criterios_do_paragrafo_10_viram_perguntas_com_opcoes_e_ids_unicos()
    {
        var questions = OrganizationReferences.DecisionQuestions.ToDictionary(question => question.Id);
        var criteria = OrganizationReferences.DecisionCriteria;

        Assert.Equal(["React", "Angular", "Manter o que existe"], questions[StackChecks.FrontendQuestion].Options);
        Assert.Equal([".NET", "outra"], questions[StackChecks.BackendQuestion].Options);
        Assert.Equal(["Leve", "Completo"], questions["10.7"].Options);
        Assert.Equal(criteria.Count, criteria.Select(criterion => criterion.Id).Distinct().Count());
        var team = criteria.Single(criterion => criterion.Id == "10.4-conhecimento-da-equipe");
        Assert.Equal([".NET", "outra"], team.Options.Select(option => option.Name));
        // A célula "-" diz que o fator não favorece aquela opção.
        Assert.Equal(["outra"], criteria.Single(criterion => criterion.Id == "10.4-ia-ml-ciencia-de-dados").Options.Select(option => option.Name));
        Assert.Equal(["Completo"], criteria.Single(criterion => criterion.Id == "10.7-varios-bancos-ou-processos").Options.Select(option => option.Name));
    }

    [Fact]
    public void Arquitetura_que_cita_os_criterios_passa_em_todas_as_verificacoes()
    {
        var checks = IdeaChecks.Architecture(Architecture(), Spec());

        Assert.All(checks, check => Assert.True(check.Passed, $"{check.Id}: {check.Detail}"));
        Assert.Contains(checks, check => check.Id == "consistent");
    }

    public static TheoryData<string, string, Action<JsonObject>> WrongArchitectures => new()
    {
        { "critério que não existe no catálogo", "factors", a => a["factors"]![0]!["criterion"] = "10.4-preferencia-pessoal" },
        { "opção que o critério não tem", "factors", a => a["factors"]![0]!["favors"] = "Go" },
        { "opção que a linha marca com -", "factors", a => a["factors"]![1]!["criterion"] = "10.4-ia-ml-ciencia-de-dados" },
        { "fator sem fato", "factors", a => a["factors"]![0]!["fact"] = "" },
        { "fator repetido", "factors", a => a["factors"]![1]!["id"] = "F1" },
        { "escolha sem critério nem requisito", "criteria", a => { Stack(a, 2).Remove("requirements"); } },
        { "requisito que não existe", "criteria", a => Stack(a, 2)["requirements"] = new JsonArray("REQ-99") },
        { "fator de outra pergunta", "criteria", a => Stack(a, 0)["factors"] = new JsonArray("F1", "F3") },
        { "opção que a pergunta não tem", "criteria", a => Stack(a, 0)["option"] = "Go" },
        { "backend sem a pergunta 10.4", "questions", a => { Stack(a, 0).Remove("question"); } },
        { "frontend por outra pergunta", "questions", a => Stack(a, 1)["question"] = "10.7" },
        { "opção contra os fatores", "consistent", a => Stack(a, 0)["option"] = "outra" },
        { "empate sem exceção", "consistent", a =>
            {
                a["factors"]!.AsArray().Add(new JsonObject { ["id"] = "F4", ["criterion"] = "10.4-ia-ml-ciencia-de-dados", ["fact"] = "Classificação de pedidos por ML.", ["favors"] = "outra" });
                a["factors"]!.AsArray().Add(new JsonObject { ["id"] = "F5", ["criterion"] = "10.4-front-e-back-na-mesma-linguagem-bff-fino", ["fact"] = "O painel é um BFF fino.", ["favors"] = "outra" });
                Stack(a, 0)["factors"] = new JsonArray("F1", "F2", "F4", "F5");
            } },
        { "sem alternativa descartada", "tradeoffs", a => Stack(a, 0)["alternatives"] = new JsonArray() },
        { "alternativa sem motivo", "tradeoffs", a => Stack(a, 1)["alternatives"]![0]!["whyNot"] = "" },
        { "sem trade-off", "tradeoffs", a => { Stack(a, 2).Remove("tradeoffs"); } }
    };

    [Theory]
    [MemberData(nameof(WrongArchitectures))]
    public void Cada_arquitetura_errada_cai_na_verificacao_certa(string caso, string expected, Action<JsonObject> spoil)
    {
        var architecture = Architecture();
        spoil(architecture);

        Assert.True(Failed(architecture).Contains(expected), $"{caso}: falhou {string.Join(", ", Failed(architecture))}");
    }

    [Fact]
    public void Mesmos_fatores_levam_a_mesma_escolha_e_quem_decide_diferente_escreve_a_excecao()
    {
        var factors = StackChecks.For(Architecture(), Spec());
        var other = Architecture();
        Stack(other, 0)["choice"] = "ASP.NET Core 10 com Minimal APIs";
        Stack(other, 0)["reason"] = "Outra redação do mesmo motivo.";
        var against = Architecture();
        Stack(against, 0)["option"] = "outra";
        Stack(against, 0)["choice"] = "Go";
        var explained = against.DeepClone().AsObject();
        Stack(explained, 0)["exception"] = "O cliente exige binário único sem runtime; a equipe aceita aprender Go.";

        Assert.Empty(Failed(other));
        Assert.Equal(factors.Select(check => check.Passed), StackChecks.For(other, Spec()).Select(check => check.Passed));
        Assert.Equal(["consistent"], Failed(against));
        Assert.Empty(Failed(explained));
    }

    [Fact]
    public void Especificacao_sem_requisito_nao_funcional_nao_passa()
    {
        var spec = Spec();
        spec["nonFunctional"] = new JsonArray();

        Assert.False(IdeaChecks.Specification(spec).Single(check => check.Id == "nonfunctional").Passed);
        Assert.True(IdeaChecks.Specification(Spec()).Single(check => check.Id == "nonfunctional").Passed);
    }

    [Fact]
    public void O_documento_da_arquitetura_traz_criterio_descarte_e_trade_off_e_a_stack_continua_legivel()
    {
        var document = IdeaDocuments.Render(new IdeaArtifacts(Idea, Spec(), Architecture(), Backlog()), Mariliz.Core.Policy.HarnessNaming.Spock)[1].Content;

        Assert.Contains("- **backend**: ASP.NET Core. Equipe já conhece. Critério: pergunta 10.4 do catálogo, opção .NET, fatores F1, F2. Atende a NFR-01. Descartado: Node/TS (A equipe não trabalha com Node.). Trade-off: Imagem do contêiner maior que a de um serviço em Go.", document);
        Assert.Contains("## Fatores da escolha", document);
        Assert.Contains("- **F1** (10.4-conhecimento-da-equipe, favorece .NET): A equipe trabalha com .NET.", document);
        var stack = Mariliz.Application.Harness.ArchitectureDocuments.ParseStack(document);
        Assert.Equal(["ASP.NET Core", "React com Vite", "PostgreSQL"], stack.Select(item => item.Choice));
    }

    [Fact]
    public void O_prompt_do_modo_chave_leva_os_criterios_do_catalogo()
    {
        var request = IdeaPrompts.Request(IdeaStages.Architecture, new IdeaArtifacts(Idea, Spec(), null, null), null);

        Assert.Contains("- 10.4-conhecimento-da-equipe: favorece \".NET\" quando Equipe .NET", request.Messages[0].Content);
        Assert.Contains("Não recomende .NET por padrão", request.Messages[0].Content);
    }

    private void Write(JsonNode design) =>
        _project.Write(".spock/project-design.json", design.ToJsonString());

    private ProjectDesign Read() => ProjectDesigns.Read(new TestFilesFactory().Open(_project.Root));

    [Fact]
    public void Arquivo_da_sessao_e_conferido_etapa_por_etapa_contra_a_especificacao_dele()
    {
        var missing = Read();
        _project.Write(".spock/project-design.json", "não é json");
        var broken = Read();
        Write(Design());
        var valid = Read();
        var wrong = Design();
        wrong["idea"] = "curta";
        wrong["backlog"]!["epics"]![0]!["stories"]![1]!["requirements"] = new JsonArray("REQ-99");
        wrong.Remove("architecture");
        Write(wrong);
        var invalid = Read();

        Assert.False(missing.Found);
        Assert.Equal(".spock/project-design.json", missing.Path);
        Assert.Equal(["json"], broken.Checks.Select(check => check.Id));
        Assert.True(valid.Valid);
        Assert.Equal(IdeaStages.All, valid.Stages.Select(stage => stage.Stage));
        Assert.Equal(Idea, valid.Idea);
        Assert.False(invalid.Valid);
        Assert.False(invalid.Checks.Single(check => check.Id == "idea").Passed);
        Assert.Equal(["present"], invalid.Stages[1].Checks.Select(check => check.Id));
        Assert.Equal("REQ-99", invalid.Stages[2].Checks.Single(check => check.Id == "traces").Detail);
    }

    [Fact]
    public async Task O_que_o_agente_escreveu_grava_pelo_mesmo_caminho_revisado_do_modo_chave()
    {
        Write(Design());
        var service = Service();
        var design = service.Design(_projectId);
        var artifacts = ProjectDesigns.Artifacts(design)!;

        var plan = service.Plan(_projectId, artifacts);
        var run = service.StartSave(_projectId, artifacts, plan.PlanId);
        await run.Completion!;

        Assert.Equal(RunStates.Completed, run.State);
        Assert.Contains("Critério: pergunta 10.4", File.ReadAllText(Path.Combine(_project.Root, ".spock", "arquitetura", "arquitetura.md")));
    }

    private IdeaArchitectService Service() =>
        new(_workspace, new ModelGateway(new NoModel(), new NoTransport()), new TestFilesFactory(), new FileRunStore(_project.DataDir),
            new FakeClock { UtcNow = Now }, new RunTracker());

    private sealed class NoModel : IModelSettings
    {
        public ModelStatus Status() => new(false, "");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = "";
            return false;
        }
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException();
    }

    private ProjectDesignTools Tools() => new(() => _workspace, new TestFilesFactory(), _project.Root, McpScope.Of(_projectId));

    private McpTool Tool(string name) => Tools().Tools().Single(tool => tool.Info.Name == name);

    private static Task<string> Call(McpTool tool) => tool.Call(new McpCall([], "teste", CancellationToken.None));

    [Fact]
    public async Task Preparar_entrega_formato_criterios_e_caminho_completo_sem_gravar_nada()
    {
        var text = await Call(Tool(ProjectDesignTools.PrepareTool));

        Assert.Contains($"Grave um único arquivo JSON em {Path.Combine(_project.Root, ".spock", "project-design.json")}", text);
        Assert.Contains("- 10.4-conhecimento-da-equipe: favorece \".NET\"", text);
        Assert.Contains("- 10.1-react: favorece \"React\"", text);
        Assert.Contains("spock_project_design_check", text);
        Assert.False(File.Exists(Path.Combine(_project.Root, ".spock", "project-design.json")));
        Assert.All(Tools().Tools(), tool => Assert.False(tool.Info.OpenWorld));
    }

    [Fact]
    public async Task Conferir_diz_o_que_falta_e_so_libera_seguir_os_documentos_depois_que_a_pessoa_grava()
    {
        var check = Tool(ProjectDesignTools.CheckTool);
        var nothing = await Call(check);
        var wrong = Design();
        Stack(wrong["architecture"]!.AsObject(), 0)["option"] = "outra";
        Write(wrong);
        var failing = await Call(check);
        Write(Design());
        var waiting = await Call(check);
        var service = Service();
        var artifacts = ProjectDesigns.Artifacts(service.Design(_projectId))!;
        await service.StartSave(_projectId, artifacts, service.Plan(_projectId, artifacts).PlanId).Completion!;
        var saved = await Call(check);

        Assert.Contains("Nenhum arquivo gravado em", nothing);
        Assert.Contains("1 de ", failing);
        Assert.Contains("Arquitetura: A opção de cada escolha é a que os fatores citados favorecem", failing);
        Assert.Contains("(backend)", failing);
        Assert.Contains("passa nas ", waiting);
        Assert.Contains("Peça à pessoa para abrir Da ideia ao projeto", waiting);
        Assert.Contains("já foram gravados pela pessoa", saved);
    }

    // A interface lê a mesma fixture no Zod.
    [Fact]
    public void Contrato_do_projeto_pela_sessao_tem_fixture_gerada_pelo_motor()
    {
        var design = Design();
        Stack(design["architecture"]!.AsObject(), 0)["option"] = "outra";
        Write(design);
        var session = new EngineSession("test", ideas: Service());
        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.ProjectDesign,
            ["params"] = new JsonObject { ["projectId"] = _projectId }
        }.ToJsonString()).Response;
        var result = response["result"]?.AsObject() ?? throw new InvalidOperationException(response.ToJsonString());

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "idea", "design.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "project.design: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
