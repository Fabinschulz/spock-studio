using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

/// <summary>
/// Inventário de MCP: o que o <c>.mcp.json</c> do projeto declara, o que cada servidor roda e onde há credencial escrita no
/// arquivo. O valor encontrado nunca pode sair: nem na resposta, nem no que a tela mostra.
/// </summary>
public class McpInventoryTests
{
    // Valores de teste com formato de credencial, sem valor real.
    private const string GithubToken = "ghp_1234567890abcdefghijklmnopqrstuvwxyz12";
    private const string UnknownFormat = "zq-9981-aaaa-bbbb";

    private static McpInventory Read(TempProject project, out string projectId)
    {
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        return new McpInventoryService(workspace, new TestFilesFactory()).Read(projectId);
    }

    /// <summary>Troca os marcadores pelos valores de teste: assim o JSON não precisa de interpolação.</summary>
    private static string Fill(string json) => json.Replace("@GH@", GithubToken).Replace("@UNK@", UnknownFormat);

    private static McpInventory Read(string mcpJson)
    {
        using var project = TempProject.Dotnet();
        project.Write(".mcp.json", Fill(mcpJson));
        return Read(project, out _);
    }

    private static string Serialized(McpInventory inventory) => JsonSerializer.Serialize(inventory, IntegrationJson.Options);

    [Fact]
    public void Projeto_sem_mcp_json_diz_que_nao_ha_arquivo()
    {
        using var project = TempProject.Dotnet();

        var inventory = Read(project, out var projectId);

        Assert.Equal((projectId, false, true), (inventory.ProjectId, inventory.Present, inventory.Readable));
        Assert.Empty(inventory.Servers);
    }

    [Fact]
    public void Servidor_local_mostra_o_programa_e_as_variaveis_que_vem_do_ambiente()
    {
        var inventory = Read("""{"mcpServers":{"github":{"command":"C:\\tools\\npx.cmd","args":["-y","@acme/server"],"env":{"GITHUB_TOKEN":"${GITHUB_TOKEN}","LOG_LEVEL":"debug"}}}}""");

        var server = Assert.Single(inventory.Servers);
        Assert.Equal(("github", "stdio", "npx.cmd"), (server.Name, server.Transport, server.Runs));
        Assert.Equal([("GITHUB_TOKEN", true), ("LOG_LEVEL", false)], server.Env.Select(item => (item.Name, item.Referenced)));
        Assert.Empty(server.LiteralSecrets);
    }

    [Fact]
    public void Credencial_com_formato_conhecido_escrita_no_arquivo_e_apontada_sem_mostrar_o_valor()
    {
        var inventory = Read("""{"mcpServers":{"github":{"command":"npx","env":{"GITHUB_TOKEN":"@GH@"}}}}""");

        var secret = Assert.Single(Assert.Single(inventory.Servers).LiteralSecrets);
        Assert.Equal("env.GITHUB_TOKEN", secret.Where);
        Assert.Contains("github-token", secret.Why);
        Assert.DoesNotContain(GithubToken, Serialized(inventory));
    }

    [Fact]
    public void O_nome_da_variavel_tambem_conta_quando_o_formato_do_valor_e_desconhecido()
    {
        var inventory = Read("""{"mcpServers":{"x":{"command":"node","env":{"MY_SERVICE_TOKEN":"@UNK@","REGION":"sa-east-1"}}}}""");

        var server = Assert.Single(inventory.Servers);
        Assert.Equal(["env.MY_SERVICE_TOKEN"], server.LiteralSecrets.Select(secret => secret.Where));
        Assert.DoesNotContain(UnknownFormat, Serialized(inventory));
    }

    [Fact]
    public void Cabecalho_que_so_referencia_variavel_de_ambiente_nao_e_alerta_mas_o_literal_e()
    {
        var inventory = Read("""
            {"mcpServers":{
              "seguro":{"type":"http","url":"https://mcp.exemplo.com/v1","headers":{"Authorization":"Bearer ${API_TOKEN}"}},
              "inseguro":{"type":"http","url":"https://mcp.exemplo.com/v1","headers":{"Authorization":"Bearer @UNK@"}}
            }}
            """);

        Assert.Empty(inventory.Servers.Single(server => server.Name == "seguro").LiteralSecrets);
        Assert.Equal(["headers.Authorization"], inventory.Servers.Single(server => server.Name == "inseguro").LiteralSecrets.Select(secret => secret.Where));
        Assert.DoesNotContain(UnknownFormat, Serialized(inventory));
    }

    [Fact]
    public void Servidor_remoto_mostra_so_o_servidor_sem_caminho_consulta_nem_usuario_e_senha()
    {
        var inventory = Read("""{"mcpServers":{"remoto":{"type":"sse","url":"https://usuario:senha-longa-123@mcp.exemplo.com/caminho/privado?api_key=@UNK@"}}}""");

        var server = Assert.Single(inventory.Servers);
        Assert.Equal(("sse", "mcp.exemplo.com"), (server.Transport, server.Runs));
        Assert.Equal(["url"], server.LiteralSecrets.Select(secret => secret.Where));
        var text = Serialized(inventory);
        Assert.DoesNotContain("senha-longa-123", text);
        Assert.DoesNotContain("caminho/privado", text);
        Assert.DoesNotContain(UnknownFormat, text);
    }

    [Fact]
    public void Credencial_num_argumento_e_apontada_pela_posicao()
    {
        var inventory = Read("""{"mcpServers":{"x":{"command":"npx","args":["-y","pacote","--token",".","@GH@"]}}}""");

        Assert.Equal(["args[4]"], Assert.Single(inventory.Servers).LiteralSecrets.Select(secret => secret.Where));
        Assert.DoesNotContain(GithubToken, Serialized(inventory));
    }

    [Fact]
    public void O_transporte_vem_do_tipo_declarado_ou_do_que_o_servidor_tem()
    {
        var inventory = Read("""{"mcpServers":{"a":{"command":"x"},"b":{"url":"https://h.exemplo.com"},"c":{"type":"sse","url":"https://h.exemplo.com"},"d":{},"e":"texto"}}""");

        Assert.Equal(["stdio", "http", "sse", "unknown", "unknown"], inventory.Servers.Select(server => server.Transport));
    }

    [Theory]
    [InlineData("{ isto nao e json")]
    [InlineData("[1,2]")]
    public void Arquivo_que_nao_e_um_json_de_servidores_vira_ilegivel_sem_derrubar_a_leitura(string text)
    {
        var inventory = Read(text);

        Assert.True(inventory.Present);
        Assert.False(inventory.Readable);
        Assert.Empty(inventory.Servers);
    }

    [Fact]
    public void Arquivo_sem_mcpServers_e_legivel_e_vazio()
    {
        var inventory = Read("""{"outra":1}""");

        Assert.True(inventory.Readable);
        Assert.Empty(inventory.Servers);
    }

    [Fact]
    public void Ler_o_inventario_nao_escreve_nada_no_projeto()
    {
        using var project = TempProject.Dotnet();
        project.Write(".mcp.json", Fill("""{"mcpServers":{"x":{"command":"npx","env":{"GITHUB_TOKEN":"@GH@"}}}}"""));
        var before = project.Fingerprint();

        Read(project, out _);

        Assert.Equal(before, project.Fingerprint());
    }

    [Fact]
    public void Nenhum_texto_do_inventario_tem_travessao_e_o_motivo_nunca_repete_o_valor()
    {
        var inventory = Read("""{"mcpServers":{"x":{"command":"npx","args":["@GH@"],"env":{"A_SECRET":"@UNK@"}}}}""");

        foreach (var secret in inventory.Servers.SelectMany(server => server.LiteralSecrets))
        {
            Assert.DoesNotContain("\u2014", secret.Why);
            Assert.DoesNotContain(GithubToken, secret.Why);
            Assert.DoesNotContain(UnknownFormat, secret.Why);
        }
    }

    [Fact]
    public void Projeto_nao_autorizado_e_recusado_e_o_metodo_responde()
    {
        using var project = TempProject.Dotnet();
        project.Write(".mcp.json", Fill("""{"mcpServers":{"x":{"command":"npx"}}}"""));
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var engine = LocalEngine.Create(project.DataDir, (_, _, _, _, _) => { }, rememberWorkspaces: false);
        var profiles = new TechnologyProfileService(engine.Configure, null!, workspace, null!, new FakeClock());
        var session = new EngineSession("test", profiles: profiles, mcp: new McpInventoryService(workspace, new TestFilesFactory()));

        JsonObject Call(string id) => session.Handle(new JsonObject
            { ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = "engineering.mcp", ["params"] = new JsonObject { ["projectId"] = id } }.ToJsonString()).Response;

        Assert.Equal("x", Call(projectId)["result"]!["servers"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("PROJECT_NOT_AUTHORIZED", Call("p-desconhecido01")["error"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public void Fixture_de_contrato_do_inventario_de_mcp()
    {
        using var project = TempProject.Dotnet();
        project.Write(".mcp.json", Fill("""
            {"mcpServers":{
              "github":{"command":"npx","args":["-y","@acme/github-server"],"env":{"GITHUB_TOKEN":"@GH@","LOG_LEVEL":"debug"}},
              "docs":{"type":"http","url":"https://mcp.exemplo.com/docs","headers":{"Authorization":"Bearer ${DOCS_TOKEN}"}}
            }}
            """));
        var inventory = Read(project, out var projectId);

        var node = JsonNode.Parse(Serialized(inventory))!;
        var pretty = node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace(projectId, "p-000000000001").ReplaceLineEndings("\n");

        Assert.DoesNotContain(GithubToken, pretty);
        var path = ContractFiles.Path_("fixtures", "engineering", "mcp.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "mcp: a resposta mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
