using System.Text.Json.Nodes;
using Mariliz.Application.Audit;
using Mariliz.Application.Management;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Core.Text;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public sealed class McpSearchTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private const string Frete = "public class Frete\n{\n    // calculo de frete do carrinho\n}\n";
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private string Authorize() => Workspace().Authorize(_project.Root).Project!.ProjectId;

    private McpServer Server(IKnowledgeSearchRuntime? search) => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }, new FileTechnologyProfileStore(_project.DataDir),
        new FileDecisionStore(_project.DataDir), _project.Root, McpScope.For(_project.Root), search, _project.DataDir, _audit).Tools());

    private static JsonObject Send(McpServer server, string method, JsonObject parameters) =>
        server.HandleAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = parameters }.ToJsonString()).GetAwaiter().GetResult()!;

    private static (string Text, bool Failed) Search(McpServer server, JsonObject arguments)
    {
        var result = Send(server, "tools/call", new JsonObject { ["name"] = "search_project", ["arguments"] = arguments })["result"]!;
        return (result["content"]![0]!["text"]!.GetValue<string>(), result["isError"]!.GetValue<bool>());
    }

    private static JsonObject Hit(string path, string hash, string body, int line = 1, string heading = "") =>
        new() { ["filePath"] = path, ["hash"] = hash, ["heading"] = heading, ["startLine"] = line, ["kind"] = "code", ["body"] = body };

    [Fact]
    public void Entrega_o_trecho_com_arquivo_e_linha_e_a_trilha_guarda_o_caminho_sem_o_pedido()
    {
        var projectId = Authorize();
        _project.Write("src/Frete.cs", Frete);
        var runtime = new FakeSearch(Hit("src/Frete.cs", Hashing.Sha256(Frete), Frete, 3, "Frete"));
        var server = Server(runtime);
        Send(server, "initialize", new JsonObject { ["clientInfo"] = new JsonObject { ["name"] = "claude-code", ["version"] = "2.1" } });

        var (text, failed) = Search(server, new JsonObject { ["query"] = "calculo de frete sigiloso", ["limit"] = 3 });

        Assert.False(failed);
        Assert.Contains("## src/Frete.cs:3 · Frete", text);
        Assert.Contains("// calculo de frete do carrinho", text);
        Assert.DoesNotContain("mudou depois da indexação", text);
        Assert.Equal(projectId, runtime.Request!["projectId"]!.GetValue<string>());
        Assert.Equal(_project.DataDir, runtime.Request["knowledgeRoot"]!.GetValue<string>());
        Assert.Equal(3, runtime.Request["limit"]!.GetValue<int>());

        var (action, detail) = Assert.Single(_audit.Entries);
        Assert.Equal(ProjectContextTools.ReadAction, action);
        Assert.Contains("src/Frete.cs", detail);
        Assert.Contains("claude-code", detail);
        Assert.DoesNotContain("sigiloso", detail);
    }

    [Fact]
    public void Arquivo_que_mudou_depois_da_indexacao_vem_com_aviso()
    {
        Authorize();
        _project.Write("src/Frete.cs", Frete + "// mudou\n");

        var (text, _) = Search(Server(new FakeSearch(Hit("src/Frete.cs", Hashing.Sha256(Frete), Frete))), new JsonObject { ["query"] = "frete" });

        Assert.Contains("O arquivo mudou depois da indexação", text);
    }

    [Fact]
    public void Trecho_que_as_regras_de_agora_barram_nao_sai_mesmo_estando_no_indice()
    {
        var projectId = Authorize();
        _project.Write(".gitignore", "segredos/\n");
        _project.Write("segredos/notas.md", "frete combinado com a transportadora\n");
        _project.Write("docs/token.md", "frete via api com ghp_abcdefghijklmnopqrstuvwxyz0123456789\n");
        _project.Write("src/Frete.cs", Frete);
        new FileDecisionStore(_project.DataDir).Save(new ManagementDecision(
            "dec-000000000001", projectId, Now, "Frete grátis", "Frete grátis acima de 200 reais", "Campanha.", [], [], []));
        var decision = Assert.Single(new DecisionMemory(Workspace(), new FileDecisionStore(_project.DataDir)).Documents(projectId));

        var (text, failed) = Search(Server(new FakeSearch(
            Hit("segredos/notas.md", Hashing.Sha256("x"), "frete combinado com a transportadora"),
            Hit("docs/token.md", Hashing.Sha256("x"), "frete via api"),
            Hit("@external/github/issue-1.md", Hashing.Sha256("x"), "issue de frete do GitHub"),
            Hit("src/Apagado.cs", Hashing.Sha256("x"), "frete antigo"),
            Hit(decision.Path, decision.Hash, decision.Text),
            Hit("src/Frete.cs", Hashing.Sha256(Frete), Frete))), new JsonObject { ["query"] = "frete" });

        Assert.False(failed);
        Assert.StartsWith("2 trechos do índice", text);
        Assert.Contains("4 trechos ficaram de fora", text);
        Assert.Contains("## Decisão registrada no Spock", text);
        Assert.Contains("## src/Frete.cs:1", text);
        foreach (var hidden in new[] { "transportadora", "frete via api", "issue de frete", "frete antigo" })
            Assert.DoesNotContain(hidden, text);
        Assert.DoesNotContain("segredos/notas.md", Assert.Single(_audit.Entries).Detail);
    }

    [Fact]
    public void Indice_vazio_ou_sem_resultado_explica_e_nao_entra_na_trilha()
    {
        Authorize();

        var empty = Search(Server(new FakeSearch { Indexed = false }), new JsonObject { ["query"] = "frete" }).Text;
        var none = Search(Server(new FakeSearch()), new JsonObject { ["query"] = "frete" }).Text;

        Assert.Contains("ainda não foi montado", empty);
        Assert.Contains("Nada no índice", none);
        Assert.Empty(_audit.Entries);
    }

    [Theory]
    [InlineData("{}", "Informe 'query'")]
    [InlineData("""{"query":"frete","limit":11}""", "vai de 1 a 10")]
    public void Pedido_invalido_volta_como_erro_da_ferramenta(string arguments, string message)
    {
        Authorize();

        var (text, failed) = Search(Server(new FakeSearch()), JsonNode.Parse(arguments)!.AsObject());

        Assert.True(failed);
        Assert.Contains(message, text);
    }

    [Fact]
    public void Sem_runtime_de_busca_diz_que_nao_esta_disponivel()
    {
        Authorize();

        var (text, failed) = Search(Server(null), new JsonObject { ["query"] = "frete" });

        Assert.True(failed);
        Assert.Contains("não está disponível", text);
    }

    [Fact]
    public void A_trilha_do_projeto_mostra_o_que_a_ide_leu()
    {
        var projectId = Authorize();
        _project.Write("src/Frete.cs", Frete);
        var server = Server(new FakeSearch(Hit("src/Frete.cs", Hashing.Sha256(Frete), Frete)));
        Send(server, "initialize", new JsonObject { ["clientInfo"] = new JsonObject { ["name"] = "Cursor" } });
        Search(server, new JsonObject { ["query"] = "frete" });

        var trail = new ProjectAuditService(Workspace(), new FakeClock { UtcNow = Now }, new FileRunStore(_project.DataDir), _audit)
            .Read(projectId, [AuditCategories.Context], null, null);

        var entry = Assert.Single(trail.Entries);
        Assert.Equal("Trechos do índice entregues à IDE", entry.Title);
        Assert.Equal("1 trecho de 1 arquivo ou decisão: src/Frete.cs. Cliente MCP informado: Cursor.", entry.Detail);
    }

    [Fact]
    public void Nome_de_cliente_estranho_nao_vai_para_a_trilha()
    {
        Authorize();
        _project.Write("src/Frete.cs", Frete);
        var server = Server(new FakeSearch(Hit("src/Frete.cs", Hashing.Sha256(Frete), Frete)));
        Send(server, "initialize", new JsonObject { ["clientInfo"] = new JsonObject { ["name"] = "x\n## falso" } });

        Search(server, new JsonObject { ["query"] = "frete" });

        Assert.DoesNotContain("client", Assert.Single(_audit.Entries).Detail);
    }

    // O contrato de ponta a ponta: o índice montado pelo Python do aplicativo é o que a busca da IDE lê.
    [Fact]
    public async Task Busca_de_verdade_le_o_indice_que_o_aplicativo_montou()
    {
        _project.Write("src/Frete.cs", Frete);
        var projectId = Authorize();
        var runtime = new LoopbackAiRuntime();
        try
        {
            var sources = new JsonArray(KnowledgeSources.Select(new TestFilesFactory().Open(_project.Root), new Dictionary<string, string>())
                .Select(source => (JsonNode)new JsonObject { ["path"] = source.Path, ["hash"] = source.Hash, ["text"] = source.Text }).ToArray());
            await runtime.PreviewContextAsync(new JsonObject
            {
                ["schema"] = "ai.context.preview.v1",
                ["projectId"] = projectId,
                ["goal"] = "frete",
                ["context"] = new JsonObject { ["stacks"] = new JsonArray(), ["knowledgeRoot"] = _project.DataDir, ["sources"] = sources }
            }, CancellationToken.None);

            var (text, failed) = Search(Server(runtime), new JsonObject { ["query"] = "calculo de frete do carrinho" });

            Assert.False(failed);
            Assert.Contains("## src/Frete.cs:", text);
            Assert.Contains("calculo de frete do carrinho", text);
            Assert.DoesNotContain("mudou depois da indexação", text);
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
    }

    private sealed class FakeSearch(params JsonObject[] hits) : IKnowledgeSearchRuntime
    {
        public bool Indexed { get; init; } = true;
        public JsonObject? Request { get; private set; }

        public Task<JsonObject> SearchKnowledgeAsync(JsonObject request, CancellationToken ct)
        {
            Request = request;
            return Task.FromResult(new JsonObject
            {
                ["schema"] = "ai.knowledge.search.ack.v1",
                ["indexed"] = Indexed,
                ["hits"] = new JsonArray(hits.Select(hit => (JsonNode)hit.DeepClone()).ToArray())
            });
        }
    }
}
