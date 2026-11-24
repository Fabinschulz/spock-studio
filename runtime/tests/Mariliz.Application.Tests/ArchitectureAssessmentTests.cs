using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Execution;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// W4-1 no modo Sessão: o agente avalia a arquitetura e grava no projeto; o motor não chama modelo e confere que cada
// afirmação se apoia em arquivo, REF-* ou achado que existe. O "dataset" é a lista de avaliações erradas de propósito: cada
// uma tem de cair na checagem certa, e a correta tem de passar em todas.
public sealed class ArchitectureAssessmentTests : IDisposable
{
    private const string AssessmentPath = ".spock/architecture-assessment.json";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = new();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly string _projectId;

    public ArchitectureAssessmentTests()
    {
        Csproj("Svc.Domain/Svc.Domain.csproj");
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        Csproj("Svc.Infrastructure/Svc.Infrastructure.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj"));
        Csproj("Svc.Api/Svc.Api.csproj", Reference(@"..\Svc.Application\Svc.Application.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        _project.Write("Svc.Application/Pedidos/CriarPedido.cs", "namespace Svc.Application.Pedidos;\npublic sealed class CriarPedido { }\n");
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private void Csproj(string path, string items = "") =>
        _project.Write(path, $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{items}</ItemGroup></Project>");

    private static string Reference(string include) => $"<ProjectReference Include=\"{include}\" />";

    private static string DotnetReference => OrganizationReferences.Patterns.First(pattern => pattern.Technologies.Contains("dotnet")).Id;

    private ArchitectureReport Analyze() => ArchitectureAnalyzer.Analyze(_projectId, new TestFilesFactory().Open(_project.Root), Now);

    private ArchitectureAssessment? Read() => ArchitectureAssessments.Read(new TestFilesFactory().Open(_project.Root), Analyze());

    private static JsonObject Valid() => new()
    {
        ["summary"] = "Camadas separadas por projeto; a aplicação depende da infraestrutura, o que inverte a direção esperada.",
        ["conventions"] = new JsonArray(new JsonObject
        {
            ["title"] = "Um projeto por camada, com o nome da camada no sufixo",
            ["files"] = new JsonArray("Svc.Domain/Svc.Domain.csproj", "Svc.Api")
        }),
        ["divergences"] = new JsonArray(new JsonObject
        {
            ["title"] = "Aplicação referencia a infraestrutura",
            ["classification"] = "risk",
            ["description"] = "Svc.Application conhece detalhes de infraestrutura; trocar o banco obriga a mexer nos casos de uso.",
            ["files"] = new JsonArray("Svc.Application/Svc.Application.csproj", "Svc.Application/Pedidos/CriarPedido.cs:2"),
            ["references"] = new JsonArray(DotnetReference),
            ["findings"] = new JsonArray(ArchitectureFindingCodes.DependencyDirection),
            ["recommendation"] = "Criar a interface do repositório na aplicação e mover a implementação para a infraestrutura, um caso de uso por vez."
        })
    };

    private void Write(JsonObject assessment) => _project.Write(AssessmentPath, assessment.ToJsonString());

    private static JsonObject Divergence(JsonObject assessment) => assessment["divergences"]![0]!.AsObject();

    [Fact]
    public void Sem_avaliacao_gravada_nao_ha_o_que_mostrar()
    {
        Assert.Null(Read());
    }

    [Fact]
    public void Avaliacao_apoiada_em_arquivo_referencia_e_achado_passa_em_todas()
    {
        Write(Valid());

        var assessment = Read()!;

        Assert.True(assessment.Valid, string.Join("; ", assessment.Checks.Where(c => !c.Passed).Select(c => $"{c.Id}: {c.Detail}")));
        Assert.Equal(AssessmentPath, assessment.Path);
        var divergence = Assert.Single(assessment.Divergences);
        Assert.Equal(DivergenceClassifications.Risk, divergence.Classification);
        Assert.Equal([DotnetReference], divergence.References);
        Assert.Equal("Um projeto por camada, com o nome da camada no sufixo", Assert.Single(assessment.Conventions).Title);
    }

    public static TheoryData<string, string> Erradas => new()
    {
        { "arquivo-inventado", "files-exist" },
        { "caminho-absoluto", "files-exist" },
        { "caminho-que-sai-do-projeto", "files-exist" },
        { "referencia-inventada", "references-exist" },
        { "achado-inventado", "findings-exist" },
        { "sem-classificacao", "classification" },
        { "classificacao-fora-da-lista", "classification" },
        { "sem-arquivo", "evidence" },
        { "sem-apoio", "grounded" },
        { "sem-recomendacao", "divergence-text" },
        { "convencao-sem-arquivo", "conventions-evidence" },
        { "sem-resumo", "summary" },
        { "divergencias-demais", "limits" }
    };

    [Theory]
    [MemberData(nameof(Erradas))]
    public void Cada_avaliacao_errada_cai_na_checagem_certa(string caso, string checagem)
    {
        var assessment = Valid();
        var divergence = Divergence(assessment);
        switch (caso)
        {
            case "arquivo-inventado": divergence["files"] = new JsonArray("Svc.Application/Repositorios/PedidoRepositorio.cs"); break;
            case "caminho-absoluto": divergence["files"] = new JsonArray(Path.Combine(_project.Root, "Svc.Api", "Svc.Api.csproj")); break;
            case "caminho-que-sai-do-projeto": divergence["files"] = new JsonArray("../fora/Program.cs"); break;
            case "referencia-inventada": divergence["references"] = new JsonArray("REF-Z99"); break;
            case "achado-inventado": divergence["findings"] = new JsonArray("god-class"); break;
            case "sem-classificacao": divergence.Remove("classification"); break;
            case "classificacao-fora-da-lista": divergence["classification"] = "grave"; break;
            case "sem-arquivo": divergence["files"] = new JsonArray(); break;
            case "sem-apoio": divergence["references"] = new JsonArray(); divergence["findings"] = new JsonArray(); break;
            case "sem-recomendacao": divergence.Remove("recommendation"); break;
            case "convencao-sem-arquivo": assessment["conventions"]![0]!["files"] = new JsonArray(); break;
            case "sem-resumo": assessment.Remove("summary"); break;
            case "divergencias-demais":
                assessment["divergences"] = new JsonArray(Enumerable.Range(0, ArchitectureAssessments.MaxDivergences + 1).Select(_ => (JsonNode)divergence.DeepClone()).ToArray());
                break;
        }
        Write(assessment);

        var result = Read()!;

        Assert.False(result.Valid);
        Assert.Equal([checagem], result.Checks.Where(check => !check.Passed).Select(check => check.Id));
    }

    [Fact]
    public void Arquivo_que_nao_e_a_avaliacao_vira_checagem_falhando_e_nao_erro()
    {
        _project.Write(AssessmentPath, "isto não é JSON");
        var broken = Read()!;
        _project.Write(AssessmentPath, """{"avaliacao": []}""");
        var wrongShape = Read()!;

        Assert.Equal("json", Assert.Single(broken.Checks).Id);
        Assert.Empty(broken.Divergences);
        Assert.False(wrongShape.Valid);
    }

    [Fact]
    public void Achado_que_sumiu_do_codigo_aparece_como_citacao_que_nao_existe_mais()
    {
        Write(Valid());
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj"));

        var result = Read()!;

        Assert.Equal(["findings-exist"], result.Checks.Where(check => !check.Passed).Select(check => check.Id));
    }

    private ArchitectureAssessmentTools Tools() =>
        new(() => _workspace, new TestFilesFactory(), _project.Root, McpScope.Of(_projectId));

    private static Task<string> Call(McpTool tool, JsonObject? args = null) => tool.Call(new McpCall(args ?? [], "teste", CancellationToken.None));

    [Fact]
    public async Task Preparar_entrega_a_analise_as_referencias_e_onde_gravar_sem_gravar_nada()
    {
        var prepare = Tools().Tools().Single(tool => tool.Info.Name == ArchitectureAssessmentTools.PrepareTool);

        var text = await Call(prepare);

        Assert.Contains("Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura) [dependency-direction]", text);
        Assert.Contains($"- {DotnetReference} · ", text);
        Assert.Contains($"em {Path.Combine(_project.Root, ".spock", "architecture-assessment.json")} e chame spock_architecture_assessment_check", text);
        Assert.DoesNotContain("Já existe uma avaliação", text);
        Assert.False(File.Exists(Path.Combine(_project.Root, ".spock", "architecture-assessment.json")));
        Assert.All(Tools().Tools(), tool => Assert.False(tool.Info.OpenWorld));
    }

    [Fact]
    public async Task Conferir_diz_o_que_falta_e_depois_aponta_a_pagina_do_projeto()
    {
        var check = Tools().Tools().Single(tool => tool.Info.Name == ArchitectureAssessmentTools.CheckTool);
        var nothing = await Call(check);
        var wrong = Valid();
        Divergence(wrong)["references"] = new JsonArray("REF-Z99");
        Write(wrong);
        var failing = await Call(check);
        Write(Valid());
        var passing = await Call(check);

        Assert.Contains("Nenhuma avaliação gravada em", nothing);
        Assert.Contains("1 de 10 verificações falhando", failing);
        Assert.Contains("Toda referência citada existe na pasta Organização (REF-Z99)", failing);
        Assert.Contains("passa nas 10 verificações", passing);
        Assert.Contains("1 divergência(s) (1 risk, 0 cost, 0 style) e 1 convenção(ões)", passing);
        Assert.Contains("seção Arquitetura", passing);
    }

    // A interface lê a mesma fixture no Zod.
    [Fact]
    public void Contrato_da_arquitetura_do_projeto_tem_fixture_gerada_pelo_motor()
    {
        Write(Valid());
        var files = new TestFilesFactory().Open(_project.Root);
        var report = ArchitectureAnalyzer.Analyze("p-000000000001", files, Now);
        var result = new JsonObject
        {
            ["architecture"] = JournalJson.Node(new ProjectArchitecture(report, ArchitectureAssessments.Read(files, report), ArchitectureAssessments.PathIn(files)))
        };
        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "engineering", "architecture.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "project.architecture: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
