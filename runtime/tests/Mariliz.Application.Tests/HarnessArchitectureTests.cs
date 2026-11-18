using Mariliz.Core.Policy;
using Mariliz.Application.Harness;
using Mariliz.Application.Ideas;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Validation;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Tests;

// Um projeto que nasceu em Da ideia ao projeto ainda não tem código: o plano do Harness parte dos documentos aprovados.
public class HarnessArchitectureTests
{
    private const string Architecture = """
        # Arquitetura

        **Estilo:** Monólito modular

        Uma API com um painel web.

        ## Componentes

        - **CMP-01 API**: Recebe os pedidos. Atende a REQ-01.

        ## Stack

        - **backend**: ASP.NET Core 9. A equipe já conhece.
        - **frontend**: Next.js. Renderiza o painel no servidor.
        - **dados**: PostgreSQL. Relacional e gratuito.
        - **outro**: Python com FastAPI. Para o serviço de avisos.

        ## Riscos

        - **backend**: isto não é stack. Mitigação: nenhuma.

        """;

    private static TempProject FromIdea(string architecture = Architecture)
    {
        var project = new TempProject();
        SpecificationFixture.Install(project);
        project.Write(IdeaDocuments.SpecificationFile(HarnessNaming.Spock), "# Pedidos da loja\n\n## Requisitos\n\n- **REQ-01** (must): O sistema deve registrar o pedido.\n");
        project.Write(IdeaDocuments.ArchitectureFile(HarnessNaming.Spock), architecture);
        project.Write(IdeaDocuments.BacklogFile(HarnessNaming.Spock), "# Backlog\n\n## EPIC-01 Pedidos\n\n### US-01 Como lojista, quero ver os pedidos\n");
        return project;
    }

    private static ComponentDecision Decision(ConfigurationPlan plan, string id) => plan.Decisions.Single(decision => decision.ComponentId == id);

    [Fact]
    public void A_stack_sai_da_secao_Stack_do_documento_sem_cortar_nomes_com_ponto()
    {
        var stack = ArchitectureDocuments.ParseStack(Architecture);

        Assert.Equal(["ASP.NET Core 9", "Next.js", "PostgreSQL", "Python com FastAPI"], stack.Select(item => item.Choice));
        Assert.Equal(["dotnet", "nextjs", null, null], stack.Select(item => item.StackId));
        Assert.Equal([true, true, false, true], stack.Select(item => item.Language));
        Assert.Equal("backend", stack[0].Area);
    }

    [Fact]
    public void Linha_editada_sem_negrito_ainda_conta_e_secao_vazia_nao_inventa_stack()
    {
        var stack = ArchitectureDocuments.ParseStack("## Stack\n\n- React com TypeScript\n- \n## Dados\n- .NET\n");

        var item = Assert.Single(stack);
        Assert.Equal("React com TypeScript", item.Choice);
        Assert.Equal("react", item.StackId);
        Assert.Equal("", item.Area);
        Assert.Empty(ArchitectureDocuments.ParseStack("# Arquitetura\n\nSem stack.\n"));
    }

    [Fact]
    public async Task Projeto_sem_codigo_recebe_a_regra_dos_documentos_e_as_regras_da_stack_planejada()
    {
        using var project = FromIdea();
        var journey = new Journey(project);

        var plan = await journey.PlanAsync();

        var architecture = Decision(plan, Catalog.RuleArchitecture);
        Assert.Equal(ComponentDecisions.Enable, architecture.Decision);
        Assert.Equal(DecisionOrigins.Architecture, architecture.Origin);
        Assert.Equal([IdeaDocuments.SpecificationFile(HarnessNaming.Spock), IdeaDocuments.ArchitectureFile(HarnessNaming.Spock), IdeaDocuments.BacklogFile(HarnessNaming.Spock)], architecture.Evidence);

        foreach (var id in new[] { Catalog.RuleStackDotnet, Catalog.RuleStackJavascript })
        {
            var decision = Decision(plan, id);
            Assert.Equal(ComponentDecisions.Enable, decision.Decision);
            Assert.Equal(DecisionOrigins.Architecture, decision.Origin);
            Assert.Equal([IdeaDocuments.ArchitectureFile(HarnessNaming.Spock)], decision.Evidence);
        }
        Assert.Contains("ASP.NET Core 9", Decision(plan, Catalog.RuleStackDotnet).Justification);

        Assert.Contains(plan.Limitations, limitation => limitation.Code == LimitationCodes.NoRuleProfile && limitation.Message.Contains("Python com FastAPI (outro)"));
        Assert.Contains(plan.Observations, observation => observation.Text.Contains("sem regra de stack: PostgreSQL (dados)"));

        // A validação exige o mesmo que o plano gera: aplicado sem a regra dos documentos, o requisito falha.
        var all = Journey.ApproveAll(plan);
        var withoutRule = all with { ChangeIds = all.ChangeIds.Where(id => plan.Changes.Single(change => change.Id == id).Path != HarnessNaming.Spock.RuleArchitecture).ToList() };
        Assert.Equal("failed", Journey.Status(await journey.ApplyAsync(plan, withoutRule), EvalIds.Requirements));

        plan = await journey.PlanAsync();
        var result = await journey.ApplyAsync(plan);
        Assert.Equal("passed", Journey.Status(result, EvalIds.Requirements));
        var rule = project.Read(HarnessNaming.Spock.RuleArchitecture)!;
        foreach (var path in new[] { IdeaDocuments.SpecificationFile(HarnessNaming.Spock), IdeaDocuments.ArchitectureFile(HarnessNaming.Spock), IdeaDocuments.BacklogFile(HarnessNaming.Spock) })
            Assert.Contains(path, rule);
        Assert.Contains("ASP.NET Core 9", project.Read(HarnessNaming.Spock.RuleDotnet));
        Assert.Contains("Next.js", project.Read(HarnessNaming.Spock.RuleJavascript));
    }

    [Fact]
    public async Task Stack_detectada_no_codigo_vale_mais_que_a_planejada()
    {
        using var project = FromIdea();
        project.Write("backend/src/App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        var plan = await new Journey(project).PlanAsync();

        var dotnet = Decision(plan, Catalog.RuleStackDotnet);
        Assert.Equal(DecisionOrigins.Baseline, dotnet.Origin);
        Assert.Contains("backend/src/App/App.csproj", dotnet.Evidence);
        Assert.Equal(DecisionOrigins.Architecture, Decision(plan, Catalog.RuleStackJavascript).Origin);
    }

    [Fact]
    public async Task Editar_a_stack_no_documento_e_planejar_de_novo_tira_a_regra_que_deixou_de_valer()
    {
        using var project = FromIdea();
        var journey = new Journey(project);
        await journey.ApplyAsync(await journey.PlanAsync());
        Assert.True(project.Exists(HarnessNaming.Spock.RuleJavascript));

        project.Write(IdeaDocuments.ArchitectureFile(HarnessNaming.Spock), Architecture.Replace("- **frontend**: Next.js. Renderiza o painel no servidor.\n", ""));
        var replanned = await journey.PlanAsync();

        Assert.Equal(ComponentDecisions.Skip, Decision(replanned, Catalog.RuleStackJavascript).Decision);
        Assert.Contains(replanned.Changes, change => change.Path == HarnessNaming.Spock.RuleJavascript && change.Action == ChangeAction.Remove);
        Assert.Contains(replanned.Changes, change => change.Path == HarnessNaming.Spock.RuleDotnet && change.Action == ChangeAction.Preserve);
    }

    [Fact]
    public async Task Sem_documentos_a_regra_de_arquitetura_fica_de_fora_e_o_plano_de_um_projeto_dotnet_nao_muda()
    {
        using var project = TempProject.Dotnet();
        SpecificationFixture.Install(project);

        var plan = await new Journey(project).PlanAsync();

        var architecture = Decision(plan, Catalog.RuleArchitecture);
        Assert.Equal(ComponentDecisions.Skip, architecture.Decision);
        Assert.DoesNotContain(plan.Changes, change => change.Path == HarnessNaming.Spock.RuleArchitecture);
        Assert.Equal(DecisionOrigins.Baseline, Decision(plan, Catalog.RuleStackDotnet).Origin);
        Assert.Equal(ComponentDecisions.Skip, Decision(plan, Catalog.RuleStackJavascript).Decision);
    }
}
