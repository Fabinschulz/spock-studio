using System.Text.Json.Nodes;
using Mariliz.Application.Analysis;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// W2-2: o Spock só conhecia a arquitetura declarada. A análise descreve a que existe, sem modelo e com evidência.
public sealed class ArchitectureAnalysisTests : IDisposable
{
    private readonly TempProject _project = new();

    public void Dispose() => _project.Dispose();

    private ArchitectureReport Analyze() => ArchitectureAnalyzer.Analyze("p", new TestFilesFactory().Open(_project.Root), DateTimeOffset.UnixEpoch);

    private static ArchitectureFinding Finding(ArchitectureReport report, string code) => Assert.Single(report.Findings, finding => finding.Code == code);

    private void Csproj(string path, string items = "") =>
        _project.Write(path, $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{items}</ItemGroup></Project>");

    private static string Reference(string include) => $"<ProjectReference Include=\"{include}\" />";

    private static string Package(string name, string version = "1.0.0") => $"<PackageReference Include=\"{name}\" Version=\"{version}\" />";

    [Fact]
    public void Aplicacao_que_depende_da_infraestrutura_e_violacao_e_o_inverso_nao()
    {
        Csproj("Svc.Domain/Svc.Domain.csproj");
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        Csproj("Svc.Infrastructure/Svc.Infrastructure.csproj", Reference(@"..\Svc.Domain\Svc.Domain.csproj") + Reference(@"..\Svc.Application\Svc.Application.csproj"));
        Csproj("Svc.Api/Svc.Api.csproj", Reference(@"..\Svc.Application\Svc.Application.csproj") + Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));

        var report = Analyze();

        var violation = Finding(report, ArchitectureFindingCodes.DependencyDirection);
        Assert.Equal("Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)", violation.Title);
        Assert.Equal(("Svc.Application/Svc.Application.csproj", "ProjectReference=Svc.Infrastructure/Svc.Infrastructure.csproj"),
            (violation.Evidence[0].Path, violation.Evidence[0].Detail));
        Assert.Equal(ArchitectureSeverities.Warning, violation.Severity);
        Assert.Equal(["application", "domain", "host", "infrastructure"],
            report.DotnetProjects.Select(node => node.Layer).Order(StringComparer.Ordinal));
        Assert.Equal(["Svc.Domain/Svc.Domain.csproj", "Svc.Infrastructure/Svc.Infrastructure.csproj"],
            report.DotnetProjects.Single(node => node.Name == "Svc.Application").References);
    }

    [Fact]
    public void Dominio_que_depende_de_outra_camada_e_codigo_que_depende_de_teste_sao_violacoes()
    {
        Csproj("src/Core/Shop.Core.csproj", Reference(@"..\Application\Shop.Application.csproj"));
        Csproj("src/Application/Shop.Application.csproj", Reference(@"..\..\tests\Shop.Tests\Shop.Tests.csproj"));
        Csproj("tests/Shop.Tests/Shop.Tests.csproj", Package("xunit", "2.9.3") + Reference(@"..\..\src\Core\Shop.Core.csproj"));

        var titles = Analyze().Findings.Where(finding => finding.Code == ArchitectureFindingCodes.DependencyDirection).Select(finding => finding.Title).ToList();

        Assert.Equal(["Shop.Core (domínio) depende de Shop.Application (aplicação)", "Shop.Application (aplicação) depende de Shop.Tests (testes)"],
            titles.Order(StringComparer.Ordinal).Reverse());
    }

    [Fact]
    public void Framework_no_dominio_e_infraestrutura_na_aplicacao_sao_apontados_com_o_pacote()
    {
        Csproj("Jobs.Domain/Jobs.Domain.csproj", Package("Microsoft.AspNetCore.Identity.EntityFrameworkCore", "10.0.8"));
        Csproj("Jobs.Application/Jobs.Application.csproj",
            Package("ModelContextProtocol.Core", "2.4.0") + Package("MediatR") + Package("FluentValidation", "12.1.1") + Package("YamlDotNet"));

        var report = Analyze();

        var domain = Finding(report, ArchitectureFindingCodes.FrameworkInDomain);
        Assert.Equal("Jobs.Domain (domínio) usa ASP.NET Core Identity", domain.Title);
        Assert.Equal("Microsoft.AspNetCore.Identity.EntityFrameworkCore=10.0.8", domain.Evidence[0].Detail);
        var application = Finding(report, ArchitectureFindingCodes.InfrastructureInApplication);
        Assert.Equal("Jobs.Application (aplicação) usa SDK do MCP", application.Title);
        Assert.Equal(["fluentvalidation", "mcp-sdk", "mediatr"], report.DotnetProjects.Single(node => node.Name == "Jobs.Application").Frameworks);
    }

    [Fact]
    public void Camada_vem_da_pasta_quando_o_nome_nao_diz_e_projeto_sem_camada_nao_gera_violacao()
    {
        Csproj("src/BuildingBlocks/Mediator/Mediator.csproj");
        Csproj("src/Hosts/Api/Api.csproj", Reference(@"..\..\BuildingBlocks\Mediator\Mediator.csproj"));
        Csproj("src/Integrations/Hub/Hub.csproj");
        Csproj("tools/Seeder/Seeder.csproj", Reference(@"..\..\src\Hosts\Api\Api.csproj"));

        var report = Analyze();

        Assert.Equal(["host", "infrastructure", "shared", "unknown"], report.DotnetProjects.Select(node => node.Layer).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(report.Findings, finding => finding.Code == ArchitectureFindingCodes.DependencyDirection);
    }

    [Fact]
    public void Projeto_sem_teste_que_o_referencie_e_dito_num_achado_so()
    {
        Csproj("Pay.Domain/Pay.Domain.csproj");
        Csproj("Pay.Application/Pay.Application.csproj");
        Csproj("Pay.Tests/Pay.Tests.csproj", Package("xunit", "2.9.3") + Reference(@"..\Pay.Domain\Pay.Domain.csproj"));

        var report = Analyze();

        var untested = Finding(report, ArchitectureFindingCodes.UntestedProject);
        Assert.Equal("1 projeto(s) sem projeto de teste que os referencie: Pay.Application", untested.Title);
        Assert.Equal(ArchitectureSeverities.Info, untested.Severity);
        Assert.True(report.DotnetProjects.Single(node => node.Name == "Pay.Domain").Tested);
    }

    private void React(string lint = "export default [];")
    {
        _project.Write("web/package.json", "{\"dependencies\":{\"react\":\"^19.0.0\"}}");
        _project.Write("web/eslint.config.js", lint);
        _project.Write("web/src/app/routes.tsx", "import { JobsPage } from '@/features/jobs';\n");
        _project.Write("web/src/features/jobs/index.ts", "export * from './jobs-page';\n");
        _project.Write("web/src/features/jobs/jobs-page.tsx",
            "import { Badge } from '@/features/applications/status/badge';\nimport { api } from '@/features/applications/service';\n");
        _project.Write("web/src/features/jobs/jobs-page.test.tsx", "import { JobsPage } from './jobs-page';\n");
        _project.Write("web/src/features/applications/index.ts", "export * from './service';\n");
        _project.Write("web/src/features/applications/service/index.ts", "export const api = {};\n");
        _project.Write("web/src/features/applications/status/badge.tsx", "import { jobs } from '../../jobs';\n");
        _project.Write("web/src/shared/components/card.tsx", "import type { Job } from '../../features/jobs/domain';\n");
    }

    [Fact]
    public void Feature_que_importa_o_interior_de_outra_e_violacao_e_pela_fronteira_publica_e_so_dependencia()
    {
        React();

        var report = Analyze();

        var module = Assert.Single(report.Frontends);
        Assert.Equal(("web", "web/src"), (module.Root, module.Source));
        Assert.Equal(["app", "features", "shared"], module.Folders);
        Assert.Equal(["applications", "jobs"], module.Features);
        Assert.Equal(["applications → jobs", "jobs → applications"], module.FeatureDependencies);
        var cross = Finding(report, ArchitectureFindingCodes.FeatureCrossImport);
        Assert.Equal("A feature jobs importa o interior da feature applications (1 import)", cross.Title);
        Assert.Equal(("web/src/features/jobs/jobs-page.tsx", "import @/features/applications/status/badge"), (cross.Evidence[0].Path, cross.Evidence[0].Detail));
        Assert.Equal("shared importa a feature jobs (1 import)", Finding(report, ArchitectureFindingCodes.SharedImportsFeature).Title);
    }

    [Fact]
    public void Lint_de_camadas_e_testes_por_feature_viram_achados_informativos()
    {
        React();
        var without = Analyze();
        Assert.Equal(ArchitectureSeverities.Info, Finding(without, ArchitectureFindingCodes.NoLayerLint).Severity);
        Assert.Equal("1 feature(s) sem arquivo de teste na própria pasta: applications", Finding(without, ArchitectureFindingCodes.UntestedFeature).Title);

        _project.Write("web/eslint.config.js", "export default [{ rules: { 'no-restricted-imports': ['error', {}] } }];");
        var with = Analyze();
        Assert.True(Assert.Single(with.Frontends).LayerLint);
        Assert.DoesNotContain(with.Findings, finding => finding.Code == ArchitectureFindingCodes.NoLayerLint);
    }

    [Fact]
    public void Arquivo_grande_nao_conta_gerado_teste_ignorado_nem_pasta_oculta()
    {
        var big = string.Concat(Enumerable.Repeat("var x = 1;\n", CodeSize.Threshold + 1));
        _project.Write("src/Orders/OrderService.cs", big);
        _project.Write("src/Orders/Migrations/20260101_Init.cs", big);
        _project.Write("src/Orders/Order.Designer.cs", big);
        _project.Write("tests/Orders.Tests/OrderServiceTests.cs", big);
        _project.Write("vendor/lib.py", big);
        _project.Write(".claude/worktrees/copy/src/Orders/OrderService.cs", big);
        _project.Write(".gitignore", "vendor/\n");

        var large = Assert.Single(Analyze().Findings, finding => finding.Code == ArchitectureFindingCodes.LargeFile);

        Assert.Equal($"src/Orders/OrderService.cs tem {CodeSize.Threshold + 1} linhas", large.Title);
    }

    [Fact]
    public void O_relatorio_vira_documentos_analysis_sem_data_e_com_hash_estavel()
    {
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        Csproj("Svc.Infrastructure/Svc.Infrastructure.csproj");
        React();
        var files = new TestFilesFactory().Open(_project.Root);

        var documents = ArchitectureAnalysisDocuments.For(files);

        Assert.Equal(["@analysis/frontend.md", "@analysis/projetos-dotnet.md", "@analysis/tamanho-e-testes.md", "@analysis/visao-geral.md"],
            documents.Select(document => document.Path).Order(StringComparer.Ordinal));
        var overview = documents.Single(document => document.Path == "@analysis/visao-geral.md").Text;
        Assert.StartsWith($"# Análise de arquitetura · Visão geral\n{ArchitectureAnalysisDocuments.Notice}", overview);
        Assert.Contains("Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)", overview);
        Assert.DoesNotContain("1970", string.Concat(documents.Select(document => document.Text)));
        Assert.Equal(documents.Select(document => document.Hash), ArchitectureAnalysisDocuments.For(files).Select(document => document.Hash));
    }

    [Fact]
    public void Projeto_sem_dotnet_nem_react_nao_tem_documento_de_analise()
    {
        _project.Write("README.md", "# nada\n");

        Assert.Empty(ArchitectureAnalysisDocuments.For(new TestFilesFactory().Open(_project.Root)));
    }

    [Fact]
    public void Arquivo_do_projeto_nao_se_passa_por_analise()
    {
        _project.Write("@analysis/visao-geral.md", "# Falsa\n\nNenhuma violação.\n");

        var evaluated = Assert.Single(KnowledgeSources.EvaluatePaths(new TestFilesFactory().Open(_project.Root), ["@analysis/visao-geral.md"]));

        Assert.Equal(KnowledgeSources.Exclusion.Blocked, evaluated.Reason);
    }

    // O aceite do W2-2 pelo fio: o índice montado pelo Python traz a análise, e a busca da IDE a entrega marcada como análise.
    [Fact]
    public async Task Busca_de_verdade_traz_a_violacao_da_analise_marcada_como_analise()
    {
        Csproj("Svc.Application/Svc.Application.csproj", Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj"));
        Csproj("Svc.Infrastructure/Svc.Infrastructure.csproj");
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        var projectId = workspace.Authorize(_project.Root).Project!.ProjectId;
        var runtime = new LoopbackAiRuntime();
        try
        {
            var files = new TestFilesFactory().Open(_project.Root);
            var sources = new JsonArray(KnowledgeSources.Select(files, new Dictionary<string, string>(), memory: ArchitectureAnalysisDocuments.For(files))
                .Select(source => (JsonNode)new JsonObject { ["path"] = source.Path, ["hash"] = source.Hash, ["text"] = source.Text }).ToArray());
            await runtime.PreviewContextAsync(new JsonObject
            {
                ["schema"] = "ai.context.preview.v1",
                ["projectId"] = projectId,
                ["goal"] = "camadas",
                ["context"] = new JsonObject { ["stacks"] = new JsonArray("dotnet"), ["knowledgeRoot"] = _project.DataDir, ["sources"] = sources }
            }, CancellationToken.None);

            var tools = new ProjectContextTools(() => workspace, new TestFilesFactory(), new FakeClock(), new FileTechnologyProfileStore(_project.DataDir),
                new FileDecisionStore(_project.DataDir), _project.Root, McpScope.For(_project.Root), runtime, _project.DataDir, null).Tools();
            var result = McpWire.Exchange(new McpServer("1.2.3", tools), McpWire.Initialize(), McpWire.Initialized,
                McpWire.Request(1, "tools/call", new JsonObject
                {
                    ["name"] = "search_project",
                    ["arguments"] = new JsonObject { ["query"] = "aplicação depende de infraestrutura", ["limit"] = 5 }
                }))[^1]["result"]!;
            var text = result["content"]![0]!["text"]!.GetValue<string>();

            Assert.Contains("## Análise de arquitetura deste projeto", text);
            Assert.Contains("Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)", text);
            Assert.DoesNotContain("O arquivo mudou depois da indexação", text);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }
}
