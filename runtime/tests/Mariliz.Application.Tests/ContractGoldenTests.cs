using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

// O Zod da interface é conferido contra as mesmas fixtures. Para regenerar depois de uma mudança deliberada: MARILIZ_UPDATE_GOLDEN=1 dotnet test.
public class ContractGoldenTests
{
    private static readonly string ProposalSchemaPath = Path.Combine(ContractFiles.Root, "runtime", "Mariliz.Application", "Assets", "schemas", "harness-proposal.v1.schema.json");

    public static IEnumerable<object[]> ValidProposals() => ContractFiles.Fixtures("proposal/valid");
    public static IEnumerable<object[]> InvalidProposals() => ContractFiles.Fixtures("proposal/invalid");

    [Theory, MemberData(nameof(ValidProposals))]
    public void Proposta_valida_passa_no_schema_e_no_leitor(string name)
    {
        var node = ContractFiles.ReadFixture("proposal/valid", name);
        var schema = ContractFiles.SchemaFromPath(ProposalSchemaPath);

        Assert.True(ContractFiles.IsValid(schema, node), name);
        Assert.Empty(ProposalReader.Parse(node!.ToJsonString()).Errors);
    }

    [Theory, MemberData(nameof(InvalidProposals))]
    public void Proposta_invalida_falha_no_schema_e_no_leitor(string name)
    {
        var node = ContractFiles.ReadFixture("proposal/invalid", name);
        var schema = ContractFiles.SchemaFromPath(ProposalSchemaPath);

        Assert.False(ContractFiles.IsValid(schema, node), name);
        Assert.NotEmpty(ProposalReader.Parse(node!.ToJsonString()).Errors);
    }

    [Fact]
    public void Todo_id_do_catalogo_proponivel_esta_no_enum_do_schema_e_vice_versa()
    {
        var schema = JsonNode.Parse(File.ReadAllText(ProposalSchemaPath))!;
        var enumIds = schema["properties"]!["decisions"]!["items"]!["properties"]!["id"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).Order().ToList();

        Assert.Equal(Catalog.Proposable.Order().ToList(), enumIds);
    }

    private sealed record Scenario(string Name, JsonNode Json);

    private static async Task<List<Scenario>> BuildScenarios()
    {
        var scenarios = new List<Scenario>();

        // Sem estrutura de especificação e com conflito: cobre comando planejado, diff e conflito.
        using (var project = TempProject.Dotnet())
        {
            project.Write(".claude/agents/spock-code-reviewer.md", "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\n\n# meu\n");
            project.Write("CLAUDE.md", "# meu\n");
            // Um agente só da equipe: o plano o mantém (preserve). O CLAUDE.md não serve mais para isso: ganha o bloco do Spock (C2).
            project.Write(".claude/agents/meu-agente.md", "---\nname: meu-agente\ndescription: da equipe\ntools: Read\n---\n\n# da equipe\n");
            var journey = new Journey(project, Journey.FakeInstaller(project));
            var discovery = await journey.Service.DiscoverAsync(journey.ProjectId, CancellationToken.None);
            var plan = await journey.PlanAsync();
            scenarios.Add(new("discovery", Normalize(JournalJson.Node(discovery)!, project, journey)));
            scenarios.Add(new("plan", Normalize(JournalJson.Node(plan)!, project, journey)));
        }

        using (var project = TempProject.Dotnet())
        {
            SpecificationFixture.Install(project);
            var journey = new Journey(project);
            var plan = await journey.PlanAsync();
            var apply = await journey.ApplyAsync(plan);
            var validate = await journey.ValidateAsync();
            var revert = await journey.RevertAsync(apply["runId"]!.GetValue<string>());
            scenarios.Add(new("apply-result", Normalize(apply, project, journey)));
            scenarios.Add(new("validate-result", Normalize(validate, project, journey)));
            scenarios.Add(new("revert-result", Normalize(revert, project, journey)));
        }

        using (var project = TempProject.Dotnet())
        {
            var journey = new Journey(project, (spec, _) => Task.FromResult<Mariliz.Application.Ports.ProcessResult?>(spec.Executable == "npx" ? new Mariliz.Application.Ports.ProcessResult(1, "", "x", false, false) : null));
            var plan = await journey.PlanAsync();
            scenarios.Add(new("apply-failure", Normalize(await journey.ApplyAsync(plan), project, journey)));
        }
        return scenarios;
    }

    private static JsonNode Normalize(JsonNode node, TempProject project, Journey journey) =>
        GoldenFixtures.Normalize(node, journey.ProjectId, Path.GetFileName(project.Root));

    [Fact]
    public async Task Formato_das_respostas_do_motor_confere_com_as_fixtures_golden()
    {
        foreach (var scenario in await BuildScenarios()) GoldenFixtures.Check(scenario.Name, scenario.Json);
    }
}
