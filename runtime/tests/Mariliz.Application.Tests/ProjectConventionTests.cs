using Mariliz.Application.Analysis;
using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Validation;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Tests;

// W3-4: projeto existente recebia a referência da Organização no lugar da própria convenção. A análise de arquitetura vira uma
// regra marcada project-convention, que vale antes dos padrões.
public class ProjectConventionTests
{
    private static readonly HarnessNaming Naming = HarnessNaming.Spock;

    private static string Csproj(string sdk = "Microsoft.NET.Sdk", string items = "") =>
        $"<Project Sdk=\"{sdk}\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup>{items}</ItemGroup></Project>";

    private static string Reference(string include) => $"<ProjectReference Include=\"{include}\" />";

    private static string Package(string name, string version) => $"<PackageReference Include=\"{name}\" Version=\"{version}\" />";

    // A forma de uma API em camadas com o mediador escrito em casa, dentro do domínio, como na referência EMP-BE.
    private static TempProject ApiWithOwnMediator(string applicationPackages = "")
    {
        var project = new TempProject();
        project.Write("backend/Jobs.sln", "Microsoft Visual Studio Solution File, Format Version 12.00\n");
        project.Write("backend/src/Jobs.Domain/Jobs.Domain.csproj", Csproj());
        project.Write("backend/src/Jobs.Domain/Libs/Mediator/IMediator.cs", "public interface IMediator { }\n");
        project.Write("backend/src/Jobs.Domain/Libs/Mediator/Mediator.cs", "internal sealed class Mediator : IMediator { }\n");
        project.Write("backend/src/Jobs.Application/Jobs.Application.csproj",
            Csproj(items: Reference(@"..\Jobs.Domain\Jobs.Domain.csproj") + Package("FluentValidation", "12.1.1") + applicationPackages));
        project.Write("backend/src/Jobs.Api/Jobs.Api.csproj", Csproj("Microsoft.NET.Sdk.Web", Reference(@"..\Jobs.Application\Jobs.Application.csproj")));
        project.Write("backend/tests/Jobs.Tests/Jobs.Tests.csproj", Csproj(items: Package("xunit", "2.9.3")));
        project.Write("backend/tests/Jobs.Tests/Mediator/MediatorTests.cs", "public class MediatorTests { }\n");
        return project;
    }

    private static async Task<(Journey Journey, ConfigurationPlan Plan)> Planned(TempProject project)
    {
        var journey = new Journey(project, Journey.FakeInstaller(project));
        return (journey, await journey.PlanAsync());
    }

    private static async Task<string> Rule(TempProject project)
    {
        var (journey, plan) = await Planned(project);
        await journey.ApplyAsync(plan);
        return project.Read(Naming.RuleProjectConventions)!;
    }

    [Fact]
    public async Task Mediador_proprio_vira_convencao_e_o_agente_nao_e_levado_a_MediatR()
    {
        using var project = ApiWithOwnMediator();

        var rule = await Rule(project);

        Assert.StartsWith("# Project conventions", rule);
        Assert.Contains("classification: project-convention", rule);
        Assert.Contains("comes before the Organization patterns", rule);
        Assert.Contains("- Mediator in `backend/src/Jobs.Domain/Libs/Mediator`: use it; do not add MediatR.", rule);
        Assert.Contains("- domain: Jobs.Domain", rule);
        Assert.Contains("- application: Jobs.Application", rule);
        Assert.Contains("- host: Jobs.Api", rule);
        Assert.Contains("- Jobs.Application: fluentvalidation", rule);
        Assert.DoesNotContain("Jobs.Tests", rule);
    }

    [Fact]
    public void Com_MediatR_instalado_a_pasta_Mediator_e_so_onde_ele_e_configurado()
    {
        using var project = ApiWithOwnMediator(Package("MediatR", "12.4.1"));

        var report = ArchitectureAnalyzer.Analyze("p", new TestFilesFactory().Open(project.Root), DateTimeOffset.UnixEpoch);

        Assert.Empty(report.OwnImplementations);
    }

    [Fact]
    public void Implementacao_propria_vem_do_projeto_ou_da_pasta_e_nunca_dos_testes()
    {
        using var project = new TempProject();
        project.Write("src/BuildingBlocks/Messaging/Messaging.csproj", Csproj());
        project.Write("src/BuildingBlocks/Messaging/Envelope.cs", "public sealed record Envelope;\n");
        project.Write("tests/Resilience/RetryTests.cs", "public class RetryTests { }\n");

        var own = ArchitectureAnalyzer.Analyze("p", new TestFilesFactory().Open(project.Root), DateTimeOffset.UnixEpoch).OwnImplementations;

        var messaging = Assert.Single(own);
        Assert.Equal(("Messaging", "src/BuildingBlocks/Messaging", "MassTransit"), (messaging.Name, messaging.Folder, messaging.Replaces));
    }

    [Fact]
    public async Task Divergencia_conhecida_e_estrutura_do_frontend_entram_na_regra()
    {
        using var project = new TempProject();
        project.Write("Svc.Application/Svc.Application.csproj", Csproj(items: Reference(@"..\Svc.Infrastructure\Svc.Infrastructure.csproj")));
        project.Write("Svc.Infrastructure/Svc.Infrastructure.csproj", Csproj());
        project.Write("web/package.json", "{\"name\":\"web\",\"dependencies\":{\"react\":\"^19.0.0\"}}");
        project.Write("web/src/app/routes.tsx", "import { Jobs } from '@/features/jobs';\n");
        project.Write("web/src/features/jobs/index.ts", "export const Jobs = {};\n");

        var rule = await Rule(project);

        Assert.Contains("## Known divergences", rule);
        Assert.Contains("- Svc.Application (aplicação) depende de Svc.Infrastructure (infraestrutura)", rule);
        Assert.Contains("## Frontend in `web/src`", rule);
        Assert.Contains("- Features: jobs.", rule);
    }

    [Fact]
    public async Task Projeto_sem_dotnet_nem_react_nao_recebe_a_regra()
    {
        using var project = new TempProject();
        project.Write("ai/pyproject.toml", "[project]\nname = \"ai\"\n");

        var (_, plan) = await Planned(project);

        Assert.NotEqual(ComponentDecisions.Enable, Assert.Single(plan.Decisions, decision => decision.ComponentId == Catalog.RuleProjectConventions).Decision);
        Assert.DoesNotContain(plan.Changes, change => change.Path == Naming.RuleProjectConventions);
    }

    [Fact]
    public async Task Validacao_exige_a_regra_de_convencoes_que_o_plano_gera()
    {
        using var project = ApiWithOwnMediator();
        var (journey, plan) = await Planned(project);
        var all = Journey.ApproveAll(plan);
        var without = all with { ChangeIds = all.ChangeIds.Where(id => plan.Changes.Single(change => change.Id == id).Path != Naming.RuleProjectConventions).ToList() };

        Assert.Equal("failed", Journey.Status(await journey.ApplyAsync(plan, without), EvalIds.Requirements));
    }
}
