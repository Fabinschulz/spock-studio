using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Validation;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Tests;

// W3-1: React e Angular recebiam a mesma regra de stack. Agora cada framework recebe os padrões da pasta Organização que valem
// para ele, uma linha por padrão, gerados do catálogo.
public class PatternRuleTests
{
    private static readonly HarnessNaming Naming = HarnessNaming.Spock;

    private static ComponentDecision Decision(ConfigurationPlan plan, string id) => Assert.Single(plan.Decisions, decision => decision.ComponentId == id);

    private static async Task<(Journey Journey, ConfigurationPlan Plan)> Planned(TempProject project)
    {
        // O instalador falso deixa a estrutura de especificação instalada, como numa máquina com o npx.
        var journey = new Journey(project, Journey.FakeInstaller(project));
        return (journey, await journey.PlanAsync());
    }

    private static async Task<TempProject> Applied(TempProject project)
    {
        var (journey, plan) = await Planned(project);
        await journey.ApplyAsync(plan);
        return project;
    }

    private static TempProject Angular()
    {
        var project = new TempProject();
        project.Write("portal/package.json", "{\"name\":\"portal\",\"dependencies\":{\"@angular/core\":\"^19.0.0\"},\"scripts\":{\"test\":\"ng test\"}}");
        return project;
    }

    private static TempProject React(string extra = "")
    {
        var project = new TempProject();
        project.Write("web/package.json", "{\"name\":\"web\",\"dependencies\":{\"react\":\"^19.0.0\"" + extra + "},\"scripts\":{\"test\":\"vitest run\"}}");
        return project;
    }

    [Fact]
    public async Task Projeto_Angular_recebe_os_padroes_Angular_e_nenhum_padrao_React()
    {
        using var project = Angular();

        var (journey, plan) = await Planned(project);

        Assert.Equal(ComponentDecisions.Enable, Decision(plan, Catalog.RulePatternsAngular).Decision);
        Assert.NotEqual(ComponentDecisions.Enable, Decision(plan, Catalog.RulePatternsReact).Decision);
        var change = Assert.Single(plan.Changes, change => change.ComponentId == Catalog.RulePatternsAngular);
        Assert.Equal(Naming.PatternRule("angular"), change.Path);
        Assert.DoesNotContain(plan.Changes, item => item.Path == Naming.PatternRule("react"));

        await journey.ApplyAsync(plan);
        var rule = project.Read(Naming.PatternRule("angular"))!;
        Assert.Contains("  - \"portal/**/*.ts\"", rule);
        Assert.Contains("  - \"portal/**/*.html\"", rule);
        Assert.Contains("- `REF-A2` ", rule);
        Assert.DoesNotContain("REF-R", rule);
        Assert.Contains("analysis of 2026-11-17", rule);
        Assert.Contains("spock_architecture_reference", rule);
    }

    [Fact]
    public async Task Projeto_React_recebe_os_padroes_React_um_por_linha_e_o_aviso_do_kit_quando_o_usa()
    {
        using var plain = await Applied(React());
        var rule = plain.Read(Naming.PatternRule("react"))!;
        Assert.Contains("  - \"web/**/*.tsx\"", rule);
        Assert.Equal(12, rule.Split('\n').Count(line => line.StartsWith("- `REF-R", StringComparison.Ordinal)));
        Assert.DoesNotContain("REF-A", rule);
        Assert.DoesNotContain("@mariliz/ui-kit", rule);
        Assert.False(plain.Exists(Naming.PatternRule("angular")));
        Assert.False(plain.Exists(Naming.PatternRule("fullstack")));

        using var kit = await Applied(React(",\"@mariliz/ui-kit\":\"^0.6.1\""));
        Assert.Contains("This project uses `@mariliz/ui-kit`", kit.Read(Naming.PatternRule("react")));
    }

    [Fact]
    public async Task Padroes_de_integracao_NET_so_entram_quando_o_projeto_integra()
    {
        using var plain = await Applied(TempProject.Dotnet());
        var rule = plain.Read(Naming.PatternRule("dotnet"))!;
        Assert.Contains("- `REF-N10` ", rule);
        Assert.DoesNotContain("- `REF-N1` ", rule);
        Assert.Contains("External integrations (REF-N1 to REF-N9) are left out", rule);

        using var integrated = TempProject.Dotnet();
        integrated.Write("backend/src/Hub/Hub.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Microsoft.Extensions.Http.Resilience\" Version=\"10.0.0\" /></ItemGroup></Project>");
        var full = (await Applied(integrated)).Read(Naming.PatternRule("dotnet"))!;
        Assert.Contains("- `REF-N1` ", full);
        Assert.DoesNotContain("are left out", full);
    }

    [Fact]
    public async Task Fullstack_e_frontend_com_backend_no_mesmo_repositorio_sem_caminho()
    {
        using var project = TempProject.Dotnet();
        project.Write("web/package.json", "{\"name\":\"web\",\"dependencies\":{\"react\":\"^19.0.0\"}}");

        var rule = (await Applied(project)).Read(Naming.PatternRule("fullstack"))!;

        Assert.StartsWith("# fullstack patterns from the Organization", rule);
        Assert.Contains("- `REF-M1` ", rule);
    }

    [Fact]
    public async Task Raiz_de_ferramentas_do_monorepo_de_frontend_nao_e_backend_mas_um_servidor_Node_e()
    {
        using var tooling = React();
        tooling.Write("package.json", "{\"name\":\"monorepo\",\"private\":true,\"scripts\":{\"lint\":\"eslint .\"}}");
        var (_, plan) = await Planned(tooling);
        Assert.NotEqual(ComponentDecisions.Enable, Decision(plan, Catalog.RulePatternsFullstack).Decision);

        using var server = React();
        server.Write("api/package.json", "{\"name\":\"api\",\"dependencies\":{\"fastify\":\"^5.0.0\"}}");
        (_, plan) = await Planned(server);
        Assert.Equal(ComponentDecisions.Enable, Decision(plan, Catalog.RulePatternsFullstack).Decision);
    }

    [Fact]
    public async Task Validacao_exige_a_regra_de_padroes_que_o_plano_gera()
    {
        using var project = Angular();
        var (journey, plan) = await Planned(project);
        var all = Journey.ApproveAll(plan);
        var without = all with { ChangeIds = all.ChangeIds.Where(id => plan.Changes.Single(change => change.Id == id).Path != Naming.PatternRule("angular")).ToList() };

        Assert.Equal("failed", Journey.Status(await journey.ApplyAsync(plan, without), EvalIds.Requirements));
    }

    [Fact]
    public async Task Excluida_no_spock_yaml_a_regra_de_padroes_nao_e_gerada()
    {
        using var project = await Applied(Angular());
        var declaration = project.Read(Naming.Declaration)!;
        project.Write(Naming.Declaration, declaration.Replace("  exclude: []", $"  exclude:\n    - {Catalog.RulePatternsAngular}"));

        var (_, plan) = await Planned(project);

        Assert.Equal(ComponentDecisions.Skip, Decision(plan, Catalog.RulePatternsAngular).Decision);
        Assert.DoesNotContain(plan.Changes, change => change.ComponentId == Catalog.RulePatternsAngular && change.Action == ChangeAction.Create);
    }
}
