using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Execution;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Mcp;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// W4-2 no modo Sessão: o agente propõe o plano a partir da avaliação (W4-1) e grava no projeto; o motor confere sem modelo e a
// pessoa aprova passo a passo. O "dataset" é a lista de planos errados de propósito: cada um cai na checagem certa, e o critério
// do roadmap (nenhuma reescrita sem custo, risco e convivência) tem dois casos próprios.
public sealed class ModernizationPlanTests : IDisposable
{
    private const string PlanPath = ".spock/modernization-plan.json";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;
    private readonly FakeClock _clock = new() { UtcNow = Now };
    private readonly FileDecisionStore _decisions;
    private readonly FileModernizationApprovalStore _approvals;
    private readonly EngineSession _session;

    public ModernizationPlanTests()
    {
        Csproj("Svc.Domain/Svc.Domain.csproj");
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        Csproj("Svc.Infrastructure/Svc.Infrastructure.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj"));
        Csproj("Svc.Api/Svc.Api.csproj", Reference(@"..\Svc.Application\Svc.Application.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        _project.Write("Svc.Application/Pedidos/CriarPedido.cs", "namespace Svc.Application.Pedidos;\npublic sealed class CriarPedido { }\n");
        _project.Write("Svc.Api/Program.cs", "var app = WebApplication.Create();\n");
        _project.Write(".spock/architecture-assessment.json", Assessment().ToJsonString());
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
        _decisions = new FileDecisionStore(_project.DataDir);
        _approvals = new FileModernizationApprovalStore(_project.DataDir);
        var decisions = new DecisionService(_workspace, _decisions, new FileRunStore(_project.DataDir),
            projectId => new EvidencePack(projectId, "Svc", Now, [], []), new MemoryAudit(), _clock);
        _session = new EngineSession("test", workspace: _workspace,
            modernization: new ModernizationService(_workspace, new TestFilesFactory(), _clock, decisions, _approvals));
    }

    public void Dispose() => _project.Dispose();

    private void Csproj(string path, string items = "") =>
        _project.Write(path, $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{items}</ItemGroup></Project>");

    private static string Reference(string include) => $"<ProjectReference Include=\"{include}\" />";

    private static JsonObject Assessment() => new()
    {
        ["summary"] = "Camadas separadas por projeto; a aplicação depende da infraestrutura.",
        ["divergences"] = new JsonArray(
            new JsonObject
            {
                ["title"] = "Aplicação referencia a infraestrutura",
                ["classification"] = "risk",
                ["description"] = "Trocar o banco obriga a mexer nos casos de uso.",
                ["files"] = new JsonArray("Svc.Application/Svc.Application.csproj"),
                ["findings"] = new JsonArray(ArchitectureFindingCodes.DependencyDirection),
                ["recommendation"] = "Interface do repositório na aplicação."
            },
            new JsonObject
            {
                ["title"] = "Sem testes",
                ["classification"] = "risk",
                ["description"] = "Nada prende o comportamento de hoje.",
                ["files"] = new JsonArray("Svc.Application/Pedidos/CriarPedido.cs"),
                ["references"] = new JsonArray(OrganizationReferences.Patterns.First(pattern => pattern.Technologies.Contains("dotnet")).Id),
                ["recommendation"] = "Projeto de testes com um teste de CriarPedido."
            })
    };

    private static JsonObject Valid() => new()
    {
        ["goal"] = "Inverter a dependência da aplicação sem mudar o contrato HTTP.",
        ["characterization"] = new JsonArray(new JsonObject
        {
            ["behavior"] = "POST /pedidos cria o pedido e devolve id, cliente e total.",
            ["files"] = new JsonArray("Svc.Api/Program.cs"),
            ["test"] = "Teste de integração do endpoint, criado no passo S1 antes de qualquer mudança."
        }),
        ["steps"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "S1", ["title"] = "Prender o comportamento de hoje", ["kind"] = "incremental", ["divergences"] = new JsonArray("D2"),
                ["change"] = "Criar Svc.Tests com um teste do endpoint POST /pedidos.", ["files"] = new JsonArray("Svc.Api/Program.cs"),
                ["dependsOn"] = new JsonArray(), ["undo"] = "Apagar o projeto Svc.Tests.", ["verification"] = "dotnet test passa.",
                ["risk"] = "Baixo: só adiciona teste."
            },
            new JsonObject
            {
                ["id"] = "S2", ["title"] = "Interface do repositório", ["kind"] = "incremental", ["divergences"] = new JsonArray("D1"),
                ["change"] = "Declarar IPedidoRepositorio e fazer CriarPedido receber a interface.",
                ["files"] = new JsonArray("Svc.Application/Pedidos/CriarPedido.cs", "Svc.Application/Svc.Application.csproj"),
                ["dependsOn"] = new JsonArray("S1"), ["undo"] = "Reverter o commit do passo.", ["verification"] = "O teste de S1 continua passando.",
                ["risk"] = "Médio: muda o registro de dependências.", ["cost"] = "Meio dia."
            })
    };

    private void Write(JsonObject plan) => _project.Write(PlanPath, plan.ToJsonString());

    private static JsonObject Step(JsonObject plan, int index) => plan["steps"]![index]!.AsObject();

    private ModernizationPlan? Read() =>
        ModernizationService.Read(_projectId, new TestFilesFactory().Open(_project.Root), Now, _approvals.List(_projectId)).Plan;

    private static string[] Failing(ModernizationPlan plan) => [.. plan.Checks.Where(check => !check.Passed).Select(check => check.Id)];

    [Fact]
    public void Sem_plano_gravado_nao_ha_o_que_mostrar()
    {
        Assert.Null(Read());
    }

    [Fact]
    public void Plano_que_caracteriza_e_se_desfaz_passo_a_passo_passa_em_todas()
    {
        Write(Valid());

        var plan = Read()!;

        Assert.True(plan.Valid, string.Join("; ", plan.Checks.Where(c => !c.Passed).Select(c => $"{c.Id}: {c.Detail}")));
        Assert.Equal(17, plan.Checks.Count);
        Assert.Equal(PlanPath, plan.Path);
        Assert.Equal(["S1", "S2"], plan.Steps.Select(step => step.Id));
        Assert.Equal(["S1"], plan.Steps[1].DependsOn);
        Assert.Matches("^[0-9a-f]{16}$", plan.Steps[0].Fingerprint);
    }

    public static TheoryData<string, string> Errados => new()
    {
        { "sem-objetivo", "goal" },
        { "sem-caracterizacao", "characterization" },
        { "caracterizacao-sem-teste", "characterization" },
        { "id-repetido", "step-ids" },
        { "id-fora-do-formato", "step-ids" },
        { "sem-mudanca", "step-text" },
        { "tipo-fora-da-lista", "kind" },
        { "sem-divergencia", "grounded" },
        { "divergencia-inventada", "divergences-exist" },
        { "sem-arquivo", "evidence" },
        { "arquivo-inventado", "files-exist" },
        { "sem-desfazer", "reversible" },
        { "sem-verificacao", "verification" },
        { "sem-risco", "risk" },
        { "reescrita-sem-convivencia", "rewrite-plan" },
        { "reescrita-disfarcada", "rewrite-declared" },
        { "depende-de-passo-posterior", "order" },
        { "passos-demais", "limits" }
    };

    [Theory]
    [MemberData(nameof(Errados))]
    public void Cada_plano_errado_cai_na_checagem_certa(string caso, string checagem)
    {
        var plan = Valid();
        var first = Step(plan, 0);
        var second = Step(plan, 1);
        switch (caso)
        {
            case "sem-objetivo": plan.Remove("goal"); break;
            case "sem-caracterizacao": plan["characterization"] = new JsonArray(); break;
            case "caracterizacao-sem-teste": plan["characterization"]![0]!.AsObject().Remove("test"); break;
            case "id-repetido": second["id"] = "S1"; break;
            case "id-fora-do-formato": second["id"] = "passo 2"; break;
            case "sem-mudanca": first.Remove("change"); break;
            case "tipo-fora-da-lista": first["kind"] = "big-bang"; break;
            case "sem-divergencia": first["divergences"] = new JsonArray(); break;
            case "divergencia-inventada": first["divergences"] = new JsonArray("D9"); break;
            case "sem-arquivo": first["files"] = new JsonArray(); break;
            case "arquivo-inventado": first["files"] = new JsonArray("Svc.Tests/PedidosTests.cs"); break;
            case "sem-desfazer": first.Remove("undo"); break;
            case "sem-verificacao": first.Remove("verification"); break;
            case "sem-risco": first.Remove("risk"); break;
            case "reescrita-sem-convivencia": second["kind"] = "rewrite"; break;
            case "reescrita-disfarcada": second["change"] = "Reescrever a camada de aplicação do zero com outro framework."; break;
            case "depende-de-passo-posterior": first["dependsOn"] = new JsonArray("S2"); break;
            case "passos-demais":
                plan["steps"] = new JsonArray(Enumerable.Range(1, ModernizationPlans.MaxSteps + 1).Select(index =>
                {
                    var step = first.DeepClone().AsObject();
                    step["id"] = $"S{index}";
                    return (JsonNode)step;
                }).ToArray());
                break;
        }
        Write(plan);

        var result = Read()!;

        Assert.False(result.Valid);
        Assert.Equal([checagem], Failing(result));
    }

    [Fact]
    public void Reescrita_declarada_com_custo_risco_e_convivencia_passa()
    {
        var plan = Valid();
        var second = Step(plan, 1);
        second["kind"] = "rewrite";
        second["change"] = "Reescrever o repositório em memória como repositório com banco, atrás da interface.";
        second["coexistence"] = "As duas implementações ficam registradas e uma chave de configuração escolhe qual vale.";
        Write(plan);

        Assert.True(Read()!.Valid);
    }

    [Fact]
    public void Sem_avaliacao_valida_o_plano_nao_tem_em_que_se_apoiar()
    {
        Write(Valid());
        File.Delete(Path.Combine(_project.Root, ".spock", "architecture-assessment.json"));

        Assert.Equal(["assessment", "divergences-exist"], Failing(Read()!));
    }

    [Fact]
    public void Arquivo_que_nao_e_o_plano_vira_checagem_falhando_e_nao_erro()
    {
        _project.Write(PlanPath, "isto não é JSON");

        var plan = Read()!;

        Assert.Equal("json", Assert.Single(plan.Checks).Id);
        Assert.Empty(plan.Steps);
    }

    private JsonObject Call(string method, JsonObject parameters) =>
        _session.Handle(new JsonObject { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = method, ["params"] = parameters }.ToJsonString()).Response;

    private JsonObject Decide(string stepId, string decision, string? rationale = null) =>
        Call(ProtocolMethods.ModernizationDecide, new JsonObject { ["projectId"] = _projectId, ["stepId"] = stepId, ["decision"] = decision, ["rationale"] = rationale });

    private static string Code(JsonObject response) => response["error"]!["code"]!.GetValue<string>();

    private static string[] Statuses(JsonObject response) =>
        [.. response["result"]!["modernization"]!["steps"]!.AsArray().Select(step => $"{step!["stepId"]}:{step["status"]}")];

    [Fact]
    public void Aprovacao_por_passo_respeita_a_ordem_e_vai_para_a_memoria_de_decisoes()
    {
        Write(Valid());

        var read = Call(ProtocolMethods.ModernizationPlan, new JsonObject { ["projectId"] = _projectId });
        var early = Decide("S2", "approved");
        var first = Decide("S1", "approved", "Teste antes de mexer.");
        var second = Decide("S2", "rejected", "Fica para depois da release.");

        Assert.Equal(["S1:pending", "S2:pending"], Statuses(read));
        Assert.Equal("D1", read["result"]!["modernization"]!["assessment"]!["divergences"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("MODERNIZATION_DEPENDENCY_PENDING", Code(early));
        Assert.Equal(["S1:approved", "S2:pending"], Statuses(first));
        Assert.Equal(["S1:approved", "S2:rejected"], Statuses(second));
        var recorded = _decisions.List(_projectId).OrderBy(decision => decision.Title).ToList();
        Assert.Equal(["Modernização S1: Prender o comportamento de hoje", "Modernização S2: Interface do repositório"], recorded.Select(d => d.Title));
        Assert.StartsWith("Aprovado o passo S1", recorded[0].Decision);
        Assert.Equal("Teste antes de mexer.", recorded[0].Rationale);
        Assert.StartsWith("Recusado o passo S2", recorded[1].Decision);
    }

    [Fact]
    public void Passo_que_muda_depois_da_aprovacao_perde_a_aprovacao()
    {
        Write(Valid());
        Decide("S1", "approved");
        var plan = Valid();
        Step(plan, 0)["change"] = "Criar Svc.Tests com dois testes do endpoint.";
        Write(plan);

        var read = Call(ProtocolMethods.ModernizationPlan, new JsonObject { ["projectId"] = _projectId });
        var blocked = Decide("S2", "approved");

        Assert.Equal(["S1:changed", "S2:pending"], Statuses(read));
        Assert.Equal("MODERNIZATION_DEPENDENCY_PENDING", Code(blocked));
    }

    [Fact]
    public void Plano_invalido_passo_inexistente_e_decisao_fora_da_lista_sao_recusados()
    {
        var plan = Valid();
        Step(plan, 0).Remove("undo");
        Write(plan);
        var invalid = Decide("S1", "approved");
        Write(Valid());
        var missing = Decide("S9", "approved");
        var wrong = Decide("S1", "talvez");

        Assert.Equal("MODERNIZATION_PLAN_INVALID", Code(invalid));
        Assert.Equal("MODERNIZATION_STEP_NOT_FOUND", Code(missing));
        Assert.Equal("INVALID_PARAMS", Code(wrong));
        Assert.Empty(_decisions.List(_projectId));
    }

    private ModernizationPlanTools Tools() =>
        new(() => _workspace, new TestFilesFactory(), _project.Root, McpScope.Of(_projectId), _approvals);

    private McpTool Tool(string name) => Tools().Tools().Single(tool => tool.Info.Name == name);

    private static Task<string> Call(McpTool tool) => tool.Call(new McpCall([], "teste", CancellationToken.None));

    [Fact]
    public async Task Preparar_exige_a_avaliacao_e_entrega_as_divergencias_pelo_id_sem_gravar_nada()
    {
        var prepare = Tool(ModernizationPlanTools.PrepareTool);
        var ready = await Call(prepare);
        File.Delete(Path.Combine(_project.Root, ".spock", "architecture-assessment.json"));
        var withoutAssessment = await Call(prepare);

        Assert.Contains("- D1 [risk] Aplicação referencia a infraestrutura", ready);
        Assert.Contains("- D2 [risk] Sem testes", ready);
        Assert.Contains($"em {Path.Combine(_project.Root, ".spock", "modernization-plan.json")} e chame spock_modernization_plan_check", ready);
        Assert.False(File.Exists(Path.Combine(_project.Root, ".spock", "modernization-plan.json")));
        Assert.Contains("ainda não existe", withoutAssessment);
        Assert.Contains(ArchitectureAssessmentTools.PrepareTool, withoutAssessment);
        Assert.All(Tools().Tools(), tool => Assert.False(tool.Info.OpenWorld));
    }

    [Fact]
    public async Task Conferir_so_libera_o_que_a_pessoa_aprovou()
    {
        var check = Tool(ModernizationPlanTools.CheckTool);
        var nothing = await Call(check);
        var wrong = Valid();
        Step(wrong, 1)["kind"] = "rewrite";
        Write(wrong);
        var failing = await Call(check);
        Write(Valid());
        var waiting = await Call(check);
        Decide("S1", "approved");
        var approved = await Call(check);

        Assert.Contains("Nenhum plano gravado em", nothing);
        Assert.Contains("1 de 17 verificações falhando", failing);
        Assert.Contains("Toda reescrita traz custo, risco e plano de convivência (S2)", failing);
        Assert.Contains("passa nas 17 verificações, 2 passo(s)", waiting);
        Assert.Contains("Nenhum passo aprovado ainda", waiting);
        Assert.Contains("- S1: aprovado", approved);
        Assert.Contains("- S2: aguardando a pessoa", approved);
        Assert.Contains("Pode executar, em ordem e rodando a verificação de cada um, só: S1.", approved);
    }

    // A interface lê a mesma fixture no Zod.
    [Fact]
    public void Contrato_do_plano_de_modernizacao_tem_fixture_gerada_pelo_motor()
    {
        Write(Valid());
        var files = new TestFilesFactory().Open(_project.Root);
        var plan = ModernizationService.Read("p-000000000001", files, Now, []).Plan!;
        var approvals = new[] { new ModernizationApproval("S1", plan.Steps[0].Fingerprint, ModernizationDecisions.Approved, "dec-000000000001", Now) };
        var result = new JsonObject { ["modernization"] = ModernizationService.Json(ModernizationService.Read("p-000000000001", files, Now, approvals)) };
        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "engineering", "modernization.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "modernization.plan: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
