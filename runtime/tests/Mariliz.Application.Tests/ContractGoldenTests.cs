using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Tests.Support;

namespace Mariliz.Application.Tests;

// O Zod da interface é conferido contra as mesmas fixtures. Para regenerar depois de uma mudança deliberada: MARILIZ_UPDATE_GOLDEN=1 dotnet test.
public class ContractGoldenTests
{
    private static string GoldenPath(string name) => ContractFiles.Path_("fixtures", "golden", name + ".json");
    private static readonly bool Update = Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1";

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
            project.Write(".claude/agents/code-reviewer.md", "---\nname: code-reviewer\ndescription: o meu\ntools: Read\n---\n\n# meu\n");
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

    // Remove o que muda a cada máquina ou execução (ids, nome da pasta temporária, versões das ferramentas).
    private static JsonNode Normalize(JsonNode node, TempProject project, Journey journey)
    {
        var text = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        text = text.Replace(journey.ProjectId, "p-000000000000").Replace(Path.GetFileName(project.Root), "projeto");
        text = System.Text.RegularExpressions.Regex.Replace(text, "run-[0-9a-f]{12}", "run-000000000000");
        text = System.Text.RegularExpressions.Regex.Replace(text, "plan-[0-9a-f]{16}", "plan-0000000000000000");
        return JsonNode.Parse(text)!;
    }

    [Fact]
    public async Task Formato_das_respostas_do_motor_confere_com_as_fixtures_golden()
    {
        foreach (var scenario in await BuildScenarios())
        {
            var path = GoldenPath(scenario.Name);
            if (Update || !File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, scenario.Json.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
                continue;
            }

            var committed = JsonNode.Parse(File.ReadAllText(path))!;
            var drift = Shape.Diff(Shape.Of(committed), Shape.Of(scenario.Json));
            Assert.True(drift.Count == 0, $"{scenario.Name}: o formato mudou ({string.Join("; ", drift.Take(8))}). Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
        }
    }

    // Chaves e tipos, ignorando valores, para tolerar dados que variam.
    private static class Shape
    {
        public static SortedDictionary<string, SortedSet<string>> Of(JsonNode? node)
        {
            var map = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            Walk(node, "$", map);
            return map;
        }

        private static void Walk(JsonNode? node, string path, SortedDictionary<string, SortedSet<string>> map)
        {
            void Add(string type) { if (!map.TryGetValue(path, out var set)) map[path] = set = []; set.Add(type); }
            Add(node switch
            {
                null => "null",
                JsonObject => "object",
                JsonArray => "array",
                JsonValue v => v.GetValueKind() switch
                {
                    JsonValueKind.String => "string",
                    JsonValueKind.Number => "number",
                    JsonValueKind.True or JsonValueKind.False => "boolean",
                    _ => "other"
                },
                _ => "other"
            });

            if (node is JsonObject o)
                foreach (var (key, value) in o) Walk(value, path + "." + key, map);
            else if (node is JsonArray a)
                foreach (var item in a) Walk(item, path + "[]", map);
        }

        public static List<string> Diff(SortedDictionary<string, SortedSet<string>> expected, SortedDictionary<string, SortedSet<string>> actual)
        {
            var problems = new List<string>();
            foreach (var (path, types) in expected)
            {
                if (!actual.TryGetValue(path, out var now)) { if (!IsArrayElementOnly(path, expected)) problems.Add($"falta {path}"); continue; }
                var missing = types.Except(now).Where(t => t != "null").ToList();
                if (missing.Count > 0 && !now.IsSupersetOf(types.Where(t => t != "null"))) problems.Add($"{path}: tipo {string.Join("/", now)} em vez de {string.Join("/", types)}");
            }
            foreach (var path in actual.Keys.Except(expected.Keys)) problems.Add($"campo novo {path}");
            return problems;
        }

        // Um caminho dentro de uma lista que está vazia no cenário atual não é divergência.
        private static bool IsArrayElementOnly(string path, SortedDictionary<string, SortedSet<string>> expected) => path.Contains("[]");
    }
}
