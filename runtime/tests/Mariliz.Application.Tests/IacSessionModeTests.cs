using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// Modo Sessão (MM-2): o agente da pessoa propõe o desenho com as ferramentas MCP e grava a proposta no projeto; o motor
// não chama modelo, só confere com as mesmas checagens e mostra na tela. Nada aqui gera .tf.
public sealed class IacSessionModeTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;

    public IacSessionModeTests()
    {
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private IacGenerationService Service() =>
        new(_workspace, new IacConfigReader(), null, new TestFilesFactory(), new FileRunStore(_project.DataDir), new FakeClock(), new RunTracker(), new MemoryAudit());

    private IacMcpTools Tools(bool withFiles = true) =>
        new(() => _workspace, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _project.Root, McpScope.Of(_projectId),
            files: withFiles ? new TestFilesFactory() : null);

    private static string Proposal(JsonNode? blueprint) => new JsonObject
    {
        ["blueprint"] = blueprint,
        ["notes"] = new JsonObject
        {
            ["services"] = new JsonArray(new JsonObject { ["name"] = "ECS Fargate", ["reason"] = "A API roda em contêiner." }),
            ["risks"] = new JsonArray("Banco em uma zona só.")
        }
    }.ToJsonString();

    private static Task<string> Call(McpTool tool, JsonObject args) => tool.Call(new McpCall(args, "teste", CancellationToken.None));

    [Fact]
    public void Sem_proposta_gravada_nao_ha_o_que_mostrar()
    {
        Assert.Null(Service().Proposal(_projectId));
    }

    [Fact]
    public void Proposta_completa_passa_nas_mesmas_checagens_da_tela()
    {
        _project.Write(".spock/iac-proposal.json", Proposal(IntegrationJson.Node(IacBlueprintSamples.Full())));

        var proposal = Service().Proposal(_projectId)!;

        Assert.Equal(".spock/iac-proposal.json", proposal.Path);
        Assert.True(proposal.Valid);
        Assert.Equal(IacBlueprintSamples.Full().Project, proposal.Blueprint!.Project);
        Assert.Equal("ECS Fargate", proposal.Notes!.Services.Single().Name);
        Assert.Equal(["Banco em uma zona só."], proposal.Notes.Risks);
    }

    [Fact]
    public void Proposta_sem_os_fatos_da_conta_e_lida_e_so_os_fatos_falham()
    {
        var design = JsonNode.Parse("""
            {"provider": "aws", "project": "loja-api", "region": "sa-east-1",
             "service": {"name": "api", "port": 8080, "healthPath": "/health", "cpu": 256, "memory": 512, "desiredCount": 1, "publicIp": false},
             "queues": ["pedidos"]}
            """);
        _project.Write(".spock/iac-proposal.json", Proposal(design));

        var proposal = Service().Proposal(_projectId)!;

        Assert.False(proposal.Valid);
        Assert.NotNull(proposal.Blueprint);
        Assert.All(proposal.Checks.Where(check => !check.Passed), check => Assert.Contains(check.Id, IacBlueprintChecks.FactIds));
    }

    [Fact]
    public void Arquivo_que_nao_e_o_desenho_vira_checagem_falhando_e_nao_erro()
    {
        _project.Write(".spock/iac-proposal.json", "isto não é JSON");
        var broken = Service().Proposal(_projectId)!;
        _project.Write(".spock/iac-proposal.json", """{"desenho": {}}""");
        var wrongShape = Service().Proposal(_projectId)!;

        Assert.Null(broken.Blueprint);
        Assert.Equal("json", Assert.Single(broken.Checks).Id);
        Assert.Null(wrongShape.Blueprint);
        Assert.False(wrongShape.Valid);
    }

    [Fact]
    public void Projeto_configurado_antes_da_troca_de_nome_le_a_proposta_na_pasta_antiga()
    {
        _project.Write(".mariliz/iac-proposal.json", Proposal(IntegrationJson.Node(IacBlueprintSamples.QueuesOnly())));

        Assert.Equal(".mariliz/iac-proposal.json", Service().Proposal(_projectId)!.Path);
    }

    [Fact]
    public void Preparar_so_existe_com_acesso_aos_arquivos_e_entrega_o_roteiro_da_nuvem()
    {
        Assert.DoesNotContain(Tools(withFiles: false).Tools(), tool => tool.Info.Name == IacMcpTools.PrepareTool);
        var prepare = Tools().Tools().Single(tool => tool.Info.Name == IacMcpTools.PrepareTool);
        Assert.False(prepare.Info.OpenWorld);
    }

    [Fact]
    public async Task Preparar_diz_onde_gravar_o_formato_e_que_a_pessoa_decide()
    {
        var prepare = Tools().Tools().Single(tool => tool.Info.Name == IacMcpTools.PrepareTool);

        var aws = await Call(prepare, new JsonObject());
        var azure = await Call(prepare, new JsonObject { ["provider"] = "azure" });
        var google = await Call(prepare, new JsonObject { ["provider"] = "gcp" });

        Assert.Contains("ECS Fargate", aws);
        Assert.Contains(".spock/iac-proposal.json", aws);
        Assert.Contains("iac_blueprint_check", aws);
        Assert.Contains("Nunca invente ids", aws);
        Assert.Contains("Você não escreve os arquivos .tf", aws);
        Assert.Contains("Container Apps", azure);
        Assert.Contains("\"provider\": \"azure\"", azure);
        Assert.Contains("Cloud Run", google);
        Assert.DoesNotContain("Já existe uma proposta", aws);
        Assert.False(File.Exists(Path.Combine(_project.Root, ".spock", "iac-proposal.json")));
    }

    [Fact]
    public async Task Preparar_avisa_da_proposta_existente_e_recusa_nuvem_fora_da_geracao()
    {
        _project.Write(".spock/iac-proposal.json", Proposal(IntegrationJson.Node(IacBlueprintSamples.Full())));
        var prepare = Tools().Tools().Single(tool => tool.Info.Name == IacMcpTools.PrepareTool);

        Assert.Contains("passa em todas as verificações", await Call(prepare, new JsonObject()));
        await Assert.ThrowsAsync<McpToolException>(() => Call(prepare, new JsonObject { ["provider"] = "oracle" }));
    }

    [Fact]
    public async Task Conferir_separa_o_que_o_agente_ajusta_do_que_a_pessoa_informa()
    {
        var check = Tools().Tools().Single(tool => tool.Info.Name == IacMcpTools.BlueprintTool);

        var text = await Call(check, new JsonObject
        {
            ["blueprint"] = JsonNode.Parse("""
                {"provider": "aws", "project": "loja-api", "region": "sa-east-1",
                 "service": {"name": "api", "port": 8080, "healthPath": "/health", "cpu": 256, "memory": 4096, "desiredCount": 1}}
                """)
        });

        var design = text.IndexOf("Ajustes no desenho:", StringComparison.Ordinal);
        var facts = text.IndexOf("Fatos da conta", StringComparison.Ordinal);
        Assert.True(design >= 0 && facts > design, text);
        Assert.Contains("CPU e memória numa combinação aceita pelo Fargate", text[design..facts]);
        Assert.Contains("VPC existente", text[facts..]);
    }

    // A interface lê a mesma fixture no Zod. Desenho Azure em que o agente não sabia a subscription: só esse fato falha.
    [Fact]
    public void Contrato_da_proposta_tem_fixture_gerada_pelo_motor()
    {
        _project.Write(".spock/iac-proposal.json", Proposal(IntegrationJson.Node(IacBlueprintSamples.AzureFull() with { Account = null })));

        var result = new JsonObject { ["proposal"] = IntegrationJson.Node(Service().Proposal(_projectId)) };
        var pretty = result.ToJsonString(new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "iac", "architect-proposal.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "iac.architect.proposal: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }

    [Fact]
    public void O_motor_e_a_sessao_usam_as_mesmas_regras_do_desenho_aws()
    {
        var request = IacArchitectPrompts.Request("Uma API de pedidos com banco e uma fila.", new JsonObject(), null);

        Assert.StartsWith("Você é o arquiteto de nuvem do Spock Studio. Proponha a infraestrutura AWS", request.Messages[0].Content);
        Assert.Contains(IacArchitectPrompts.AwsDesign, request.Messages[0].Content);
        Assert.Contains(IacArchitectPrompts.AwsDesign, IacSessionGuide.For("aws", ".spock/iac-proposal.json"));
    }
}
