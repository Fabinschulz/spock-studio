using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Mcp;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

// F3: o pacote de abertura do Context Engine chega à IDE em texto e em JSON (structuredContent), com o schema de contrato.
public sealed class McpContextBootstrapTests : IDisposable
{
    private const string StableId = "spk-01M7GTEK00ABCDEFGHJKMNPQRS";
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero) };

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private McpServer Server(bool withContext = true) => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), _clock, new FileTechnologyProfileStore(_project.DataDir), new FileDecisionStore(_project.DataDir),
        _project.Root, McpScope.For(_project.Root), audit: _audit,
        context: withContext
            ? workspace => new ContextEngine(workspace, new HarnessStatusService(workspace, new TestFilesFactory(), _clock),
                new FileTechnologyProfileStore(_project.DataDir), new TiktokenTokenizers(), _clock)
            : null).Tools());

    private static JsonObject Call(McpServer server, JsonObject? arguments = null) =>
        McpWire.Exchange(server, McpWire.Initialize(client: "claude-code"), McpWire.Initialized,
            McpWire.Request(1, "tools/call", new JsonObject { ["name"] = ContextBootstrapTool.Name, ["arguments"] = arguments ?? new JsonObject() }))[^1];

    [Fact]
    public void A_ferramenta_vem_primeiro_com_o_schema_de_saida()
    {
        var tools = McpWire.Tools(Server());

        Assert.Equal(ContextBootstrapTool.Name, tools[0]!["name"]!.GetValue<string>());
        Assert.Equal("object", tools[0]!["outputSchema"]!["type"]!.GetValue<string>());
        Assert.True(tools[0]!["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        // Sem o Context Engine montado, a ferramenta não aparece em vez de falhar quando chamada.
        Assert.DoesNotContain(McpWire.Tools(Server(withContext: false)), tool => tool!["name"]!.GetValue<string>() == ContextBootstrapTool.Name);
    }

    // Aceite do F3: texto e JSON juntos (o Cursor descarta resposta só estruturada), e o JSON segue o contrato.
    [Fact]
    public void Responde_em_texto_e_em_json_que_segue_o_contrato_e_fica_na_trilha()
    {
        _project.Write("mariliz.yaml", $"schema: 1\nproject:\n  id: {StableId}\n  name: loja\nimplementationModel: mariliz\naiEnvironment: claude-code\n");
        var projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;

        var result = Call(Server())["result"]!;

        Assert.False(result["isError"]!.GetValue<bool>());
        var text = result["content"]![0]!["text"]!.GetValue<string>();
        var structured = result["structuredContent"]!;
        Assert.StartsWith("# Contexto do projeto", text);
        Assert.Equal(StableId, structured["stableId"]!.GetValue<string>());
        Assert.Equal(projectId, structured["projectId"]!.GetValue<string>());
        Assert.All(structured["items"]!.AsArray(), item => Assert.Contains(item!["text"]!.GetValue<string>(), text));
        Assert.True(ContractFiles.IsValid(ContractFiles.Schema("mcp", "spock_context_bootstrap.output.schema.json"), structured),
            structured.ToJsonString());

        var (action, detail) = Assert.Single(_audit.Entries);
        Assert.Equal(ProjectContextTools.ReadAction, action);
        Assert.Contains(ContextBootstrapTool.Name, detail);
        Assert.Contains(structured["contextVersion"]!.GetValue<string>(), detail);
        Assert.Contains("claude-code", detail);
    }

    [Fact]
    public void Orcamento_fora_da_faixa_volta_como_erro_da_ferramenta_sem_json()
    {
        Workspace().Authorize(_project.Root);

        var result = Call(Server(), new JsonObject { ["budget_tokens"] = 10 })["result"]!;

        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Null(result["structuredContent"]);
    }

    [Fact]
    public void Linha_com_cara_de_segredo_sai_redigida_tambem_no_json()
    {
        var secret = "ghp_abcdefghijklmnopqrstuvwxyz0123456789";
        var tool = McpTool.Structured(new McpToolInfo("eco", "Eco", "Eco.", new JsonObject { ["type"] = "object" }),
            _ => Task.FromResult(new McpReply("ok", new JsonObject { ["linhas"] = new JsonArray($"token={secret}", "normal") })));

        var reply = McpWire.Exchange(new McpServer("1.2.3", [tool]), McpWire.Initialize(), McpWire.Initialized,
            McpWire.Request(1, "tools/call", new JsonObject { ["name"] = "eco" }))[^1];

        Assert.DoesNotContain(secret, reply.ToJsonString());
        Assert.Equal("normal", reply["result"]!["structuredContent"]!["linhas"]![1]!.GetValue<string>());
    }

    // O schema que vai no outputSchema é o mesmo do contrato versionado; regenere com MARILIZ_UPDATE_GOLDEN=1 se mudar de propósito.
    [Fact]
    public void O_schema_de_saida_e_o_do_contrato()
    {
        var schema = ContextBootstrapTool.OutputSchema();
        schema.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        schema.Insert(1, "$id", "https://mariliz.dev/schemas/mcp/spock_context_bootstrap.output.schema.json");
        var pretty = schema.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("schemas", "mcp", "spock_context_bootstrap.output.schema.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "spock_context_bootstrap: o schema de saída mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1.");
    }
}
