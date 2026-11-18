using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// W1-4: a IDE consulta a pasta Organização por id, por tecnologia e por assunto, em texto e em JSON com contrato.
public sealed class McpArchitectureReferenceTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero) };

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private McpServer Server() => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), _clock, new FileTechnologyProfileStore(_project.DataDir), new FileDecisionStore(_project.DataDir),
        _project.Root, McpScope.For(_project.Root), audit: _audit).Tools());

    private string Authorize(params string[] stacks)
    {
        var projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        if (stacks.Length > 0)
            new FileTechnologyProfileStore(_project.DataDir).Write(new TechnologyProfile(projectId, _clock.UtcNow,
                stacks.Select(id => new ProfileTechnology(id, id, "Framework web", ["1.0"], [""], Confidence.High, [])).ToList(), [], [], [], [], []));
        return projectId;
    }

    private JsonNode Call(JsonObject arguments) =>
        McpWire.Exchange(Server(), McpWire.Initialize(client: "claude-code"), McpWire.Initialized,
            McpWire.Request(1, "tools/call", new JsonObject { ["name"] = ArchitectureReferenceTool.Name, ["arguments"] = arguments }))[^1]["result"]!;

    private static List<string> Ids(JsonNode result) =>
        result["structuredContent"]!["patterns"]!.AsArray().Select(pattern => pattern!["id"]!.GetValue<string>()).ToList();

    [Fact]
    public void A_ferramenta_aparece_so_leitura_com_schema_de_saida_e_no_catalogo_da_tela()
    {
        var tool = McpWire.Tools(Server()).Single(item => item!["name"]!.GetValue<string>() == ArchitectureReferenceTool.Name)!;

        Assert.Equal("object", tool["outputSchema"]!["type"]!.GetValue<string>());
        Assert.True(tool["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        Assert.Contains(ProjectContextTools.Catalog, info => info.Name == ArchitectureReferenceTool.Name);
    }

    [Fact]
    public void Por_id_traz_o_padrao_inteiro_com_cada_coluna_e_segue_o_contrato()
    {
        Authorize("react");

        var result = Call(new JsonObject { ["id"] = "REF-R4" });

        Assert.False(result["isError"]!.GetValue<bool>());
        var pattern = Assert.Single(result["structuredContent"]!["patterns"]!.AsArray())!;
        Assert.Equal("REF-R4", pattern["id"]!.GetValue<string>());
        Assert.Equal("reference", pattern["classification"]!.GetValue<string>());
        Assert.Contains(pattern["details"]!.AsArray(), detail => detail!["label"]!.GetValue<string>() == "Onde"
            && detail["text"]!.GetValue<string>().Contains("query-client", StringComparison.Ordinal));
        var text = result["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("## REF-R4", text);
        Assert.Contains("não é convenção deste projeto", text);
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("mcp", "spock_architecture_reference.output.schema.json"), result["structuredContent"]),
            result["structuredContent"]!.ToJsonString());
        Assert.Contains("REF-R4", Assert.Single(_audit.Entries).Detail);
    }

    [Fact]
    public void Sem_argumentos_usa_a_stack_do_projeto()
    {
        Authorize("react");

        var ids = Ids(Call(new JsonObject { ["limit"] = 20 }));

        Assert.Contains("REF-R1", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("REF-A", StringComparison.Ordinal) || id.StartsWith("REF-N", StringComparison.Ordinal));
    }

    [Fact]
    public void Tecnologia_e_assunto_trazem_o_padrao_certo_primeiro()
    {
        Authorize("react");

        var ids = Ids(Call(new JsonObject { ["technology"] = "angular", ["topic"] = "interceptor de autenticação e refresh do token" }));

        Assert.Equal("REF-A2", ids[0]);
        Assert.DoesNotContain(ids, id => id.StartsWith("REF-R", StringComparison.Ordinal));
    }

    [Fact]
    public void Id_que_nao_existe_volta_como_erro_que_o_modelo_le()
    {
        Authorize();

        var result = Call(new JsonObject { ["id"] = "REF-Z99" });

        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Contains("REF-Z99", result["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void Cada_padrao_traz_as_colunas_da_tabela_dele()
    {
        var pattern = OrganizationReferences.Patterns.Single(item => item.Id == "REF-N2");

        Assert.Equal(["Onde (NET-INT)", "Problema que resolve", "Evitar quando"], pattern.Details.Select(detail => detail.Label));
        Assert.All(pattern.Details, detail => Assert.False(string.IsNullOrWhiteSpace(detail.Text)));
    }

    // O schema que vai no outputSchema é o mesmo do contrato versionado; regenere com MARILIZ_UPDATE_GOLDEN=1 se mudar de propósito.
    [Fact]
    public void O_schema_de_saida_e_o_do_contrato()
    {
        var schema = ArchitectureReferenceTool.OutputSchema();
        schema.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        schema.Insert(1, "$id", "https://mariliz.dev/schemas/mcp/spock_architecture_reference.output.schema.json");
        var pretty = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("schemas", "mcp", "spock_architecture_reference.output.schema.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "spock_architecture_reference: o schema de saída mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1.");
    }
}
