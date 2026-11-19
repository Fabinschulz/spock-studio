using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// MV-3: combinação de versões que não roda (framework que exige Node mais novo, SDK que não compila o alvo) e a conferência
// das versões depois da tarefa, pelo MCP.
public sealed class IncompatibilityTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly ReleaseCatalog Catalog = ReleaseCatalog.Parse("""
        {
          "schemaVersion": 1,
          "generatedAt": "2026-11-18T00:00:00Z",
          "technologies": [
            { "id": "dotnet", "name": ".NET", "policy": "lifecycle", "cycle": "major.minor", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "10.0", "channel": "lts", "status": "active", "eol": "2028-11-14", "latest": "10.0.15", "language": "C# 14" },
                { "cycle": "8.0", "channel": "lts", "status": "eol", "eol": "2026-11-10", "latest": "8.0.34", "language": "C# 12" } ] },
            { "id": "node", "name": "Node.js", "policy": "lifecycle", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "26", "channel": "lts", "status": "active", "eol": "2029-04-30" },
                { "cycle": "22", "channel": "lts", "status": "maintenance", "eol": "2027-04-30" },
                { "cycle": "20", "channel": "lts", "status": "eol", "eol": "2026-04-30" },
                { "cycle": "18", "channel": "lts", "status": "eol", "eol": "2025-04-30" } ] },
            { "id": "vite", "name": "Vite", "policy": "latest-major", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [
                { "cycle": "8", "channel": "stable", "status": "active", "latest": "8.3.8", "requires": { "node": "^20.19.0 || >=22.12.0" } },
                { "cycle": "5", "channel": "stable", "status": "unknown", "latest": "5.4.21", "requires": { "node": "^18.0.0 || >=20.0.0" } } ] },
            { "id": "nextjs", "name": "Next.js", "policy": "latest-major", "cycle": "major", "source": "x", "checkedAt": "2026-11-18T00:00:00Z", "stale": false,
              "lines": [ { "cycle": "16", "channel": "stable", "status": "active", "latest": "16.4.6", "requires": { "node": ">=20.9.0" } } ] }
          ]
        }
        """);

    private readonly TempProject _project = new();
    private readonly MemoryAudit _audit = new();

    public void Dispose() => _project.Dispose();

    private static ProjectModule Module(string root, string technology, string? version, string origin = ProjectModules.Stack, string? name = null,
        params string[] evidence) =>
        new(root, technology, name ?? technology, version, origin, Confidence.High, evidence);

    private static ModuleCompatibility Find(IReadOnlyList<ProjectModule> modules, string technology, string? name = null) =>
        Assert.Single(CompatibilityAssessor.Assess(modules, Catalog, Now).Modules,
            module => module.TechnologyId == technology && (name is null || module.Name == name));

    [Theory]
    [InlineData("^20.19.0 || >=22.12.0", "18.20.4", RangeVerdict.Unsatisfied)]
    [InlineData("^20.19.0 || >=22.12.0", "21", RangeVerdict.Unsatisfied)]
    [InlineData("^20.19.0 || >=22.12.0", "20", RangeVerdict.Unknown)]
    [InlineData("^20.19.0 || >=22.12.0", "20.19", RangeVerdict.Satisfied)]
    [InlineData("^20.19.0 || >=22.12.0", "22.12.0", RangeVerdict.Satisfied)]
    [InlineData("^20.19.0 || >=22.12.0", "26", RangeVerdict.Satisfied)]
    [InlineData("^18.18.0 || ^19.8.0 || >= 20.0.0", "18.17.1", RangeVerdict.Unsatisfied)]
    [InlineData("^18.18.0 || ^19.8.0 || >= 20.0.0", "v20.11.0", RangeVerdict.Satisfied)]
    [InlineData(">=14.17", "14.17", RangeVerdict.Satisfied)]
    [InlineData(">=14.17", "14", RangeVerdict.Unknown)]
    [InlineData("~18.17", "18.17.5", RangeVerdict.Satisfied)]
    [InlineData("~18.17", "18.18.0", RangeVerdict.Unsatisfied)]
    [InlineData("16 - 18", "18.20.4", RangeVerdict.Satisfied)]
    [InlineData("<20", "20.0.0", RangeVerdict.Unsatisfied)]
    [InlineData(">20", "20.5.0", RangeVerdict.Unsatisfied)]
    [InlineData(">= 8.0.0", "lts/*", RangeVerdict.Unknown)]
    [InlineData("isto não é faixa", "20.0.0", RangeVerdict.Unknown)]
    public void Faixa_do_npm_contra_a_versao_do_projeto(string range, string version, RangeVerdict verdict) =>
        Assert.Equal(verdict, NodeRange.Evaluate(range, version));

    [Fact]
    public void Vite_que_exige_node_mais_novo_que_o_fixado_e_incompativel_e_o_agente_nao_e_mandado_trocar_versao()
    {
        var vite = Find([Module("web", "vite", "8.3.2", ProjectModules.Framework), Module("web", "node", "18.20.4", ProjectModules.Runtime, "Node.js", "web/.nvmrc: 18.20.4")], "vite");

        Assert.Equal((SupportStatuses.Incompatible, "Incompatível"), (vite.Status, vite.StatusLabel));
        Assert.Contains("exige Node ^20.19.0 || >=22.12.0", vite.Reason);
        Assert.Contains("web/.nvmrc: 18.20.4", vite.Reason);
        Assert.Contains("Não troque versões para contornar", vite.Guidance);
    }

    [Fact]
    public void Node_so_com_o_numero_maior_nao_vira_incompativel_mas_pede_a_versao_exata()
    {
        var vite = Find([Module("web", "vite", "8.3.2", ProjectModules.Framework), Module("web", "node", "20", ProjectModules.Runtime, "Node.js")], "vite");

        Assert.Equal(SupportStatuses.Current, vite.Status);
        Assert.Contains("confirme a versão exata", vite.Reason);
    }

    [Fact]
    public void O_node_da_pasta_acima_vale_e_o_fixado_vence_o_minimo_do_engines()
    {
        var modules = new[]
        {
            Module("apps/web", "nextjs", "16.0.1"),
            Module(".", "node", "18", ProjectModules.Stack, "Node.js"),
            Module(".", "node", "22.12.0", ProjectModules.Runtime, "Node.js"),
            Module("legado", "vite", "5.4.0", ProjectModules.Framework)
        };

        Assert.NotEqual(SupportStatuses.Incompatible, Find(modules, "nextjs").Status);
        Assert.NotEqual(SupportStatuses.Incompatible, Find(modules, "vite").Status);
        Assert.Equal(SupportStatuses.Incompatible, Find([Module("apps/web", "nextjs", "16.0.1"), Module(".", "node", "18.20.4", ProjectModules.Runtime, "Node.js")], "nextjs").Status);
    }

    [Fact]
    public void Sem_node_fixado_nao_ha_veredito()
    {
        var vite = Find([Module("web", "vite", "8.3.2", ProjectModules.Framework)], "vite");

        Assert.Equal(SupportStatuses.Current, vite.Status);
        Assert.DoesNotContain("Node", vite.Reason);
    }

    [Fact]
    public void Sdk_do_global_json_mais_antigo_que_o_alvo_nao_compila_salvo_com_roll_forward_de_versao_maior()
    {
        var modules = new List<ProjectModule>
        {
            Module("api", "dotnet", "net10.0"),
            Module("api", "dotnet", "8.0.428", ProjectModules.Runtime, ".NET SDK", "global.json: sdk.version=8.0.428")
        };

        var sdk = Find(modules, "dotnet", ".NET SDK");
        Assert.Equal(SupportStatuses.Incompatible, sdk.Status);
        Assert.Contains("não compila net10.0", sdk.Reason);
        Assert.Contains("10.0 ou mais novo", sdk.Reason);

        modules[1] = Module("api", "dotnet", "8.0.428", ProjectModules.Runtime, ".NET SDK", "global.json: sdk.version=8.0.428, rollForward=latestMajor");
        Assert.NotEqual(SupportStatuses.Incompatible, Find(modules, "dotnet", ".NET SDK").Status);
    }

    // ---------- conferência depois da tarefa ----------

    private TechnologyProfile Baseline(string projectId, params ProjectModule[] modules) =>
        new(projectId, Now.AddDays(-1), [], [], [], [], [], []) { Modules = modules };

    [Fact]
    public void Conferencia_aponta_versao_trocada_dependencia_nova_e_removida()
    {
        var baseline = Baseline("p-000000000001", Module("api", "dotnet", "net8.0"), Module("web", "vite", "5.4.0", ProjectModules.Framework),
            Module("web", "zod", "3.0.0", ProjectModules.Framework));
        var current = new[] { Module("api", "dotnet", "net10.0"), Module("web", "vite", "5.4.0", ProjectModules.Framework),
            Module("web", "react-query", "5.0.0", ProjectModules.Framework) };

        var report = VersionCheck.Compare("p-000000000001", baseline, current, Catalog, Now);

        Assert.False(report.Clean);
        Assert.Equal([("api", VersionCheck.Changed, "net8.0", "net10.0"), ("web", VersionCheck.Added, null, "5.0.0"), ("web", VersionCheck.Removed, "3.0.0", null)],
            report.Changes.Select(change => (change.Root, change.Kind, change.From, change.To)));
        Assert.Contains("Se a tarefa não pediu essa mudança, desfaça", report.Guidance);
    }

    [Fact]
    public void Conferencia_sem_mudanca_e_sem_incompatibilidade_e_limpa_e_sem_perfil_diz_que_nao_ha_referencia()
    {
        var modules = new[] { Module("api", "dotnet", "net10.0") };

        var clean = VersionCheck.Compare("p-000000000001", Baseline("p-000000000001", modules), modules, Catalog, Now);
        Assert.True(clean.Clean);
        Assert.Empty(clean.Changes);

        var none = VersionCheck.Compare("p-000000000001", null, modules, Catalog, Now);
        Assert.Null(none.BaselineAt);
        Assert.Contains("não há com o que comparar", none.Guidance);
    }

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private McpServer Server(bool withContext = false) => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }, new FileTechnologyProfileStore(_project.DataDir),
        new FileDecisionStore(_project.DataDir), _project.Root, McpScope.For(_project.Root), audit: _audit,
        context: withContext
            ? workspace => new ContextEngine(workspace, new HarnessStatusService(workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }),
                new FileTechnologyProfileStore(_project.DataDir), new TiktokenTokenizers(), new FakeClock { UtcNow = Now })
            : null).Tools());

    private JsonNode Call(string tool, bool withContext = false) =>
        McpWire.Exchange(Server(withContext), McpWire.Initialize(client: "claude-code"), McpWire.Initialized,
            McpWire.Request(1, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = new JsonObject() }))[^1]["result"]!;

    [Fact]
    public void Pelo_mcp_o_agente_ve_o_vite_que_ele_subiu_e_que_o_node_do_projeto_nao_roda()
    {
        _project.Write("web/package.json", """{ "name": "web", "dependencies": { "react": "^19.3.0" }, "devDependencies": { "vite": "^8.3.2" } }""");
        _project.Write("web/.nvmrc", "18.20.4\n");
        var projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        new FileTechnologyProfileStore(_project.DataDir).Write(Baseline(projectId,
            Module("web", "react", "19.3.0", ProjectModules.Stack, "React"),
            Module("web", "node", "18.20.4", ProjectModules.Runtime, "Node.js"),
            Module("web", "vite", "5.4.0", ProjectModules.Framework, "Vite")));

        var result = Call(VersionCheckTool.Name);

        Assert.False(result["isError"]!.GetValue<bool>());
        var structured = result["structuredContent"]!;
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("mcp", "spock_version_check.output.schema.json"), structured), structured.ToJsonString());
        Assert.False(structured["clean"]!.GetValue<bool>());
        var change = Assert.Single(structured["changes"]!.AsArray());
        Assert.Equal(("vite", "5.4.0", "8.3.2"), (change!["technologyId"]!.GetValue<string>(), change["from"]!.GetValue<string>(), change["to"]!.GetValue<string>()));
        var incompatible = Assert.Single(structured["incompatible"]!.AsArray());
        Assert.Equal("vite", incompatible!["technologyId"]!.GetValue<string>());
        Assert.Contains("Versões mudaram", result["content"]![0]!["text"]!.GetValue<string>());
        var (action, detail) = Assert.Single(_audit.Entries);
        Assert.Equal(ProjectContextTools.ReadAction, action);
        Assert.Contains(VersionCheckTool.Name, detail);
    }

    // Defeito do MV-1: o pacote de abertura passou a ter a seção "versions" e o schema do contrato não a aceitava.
    [Fact]
    public void Pacote_de_abertura_com_restricoes_de_versao_segue_o_contrato()
    {
        var projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        new FileTechnologyProfileStore(_project.DataDir).Write(Baseline(projectId, Module("api", "dotnet", "net8.0", ProjectModules.Stack, ".NET")));

        var structured = Call(ContextBootstrapTool.Name, withContext: true)["structuredContent"]!;

        Assert.Contains(structured["items"]!.AsArray(), item => item!["section"]!.GetValue<string>() == "versions");
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("mcp", "spock_context_bootstrap.output.schema.json"), structured), structured.ToJsonString());
    }

    [Fact]
    public void O_schema_de_saida_da_conferencia_e_o_do_contrato()
    {
        var schema = VersionCheckTool.OutputSchema();
        schema.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        schema.Insert(1, "$id", "https://mariliz.dev/schemas/mcp/spock_version_check.output.schema.json");
        var pretty = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("schemas", "mcp", "spock_version_check.output.schema.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "spock_version_check: o schema de saída mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1.");
    }
}
