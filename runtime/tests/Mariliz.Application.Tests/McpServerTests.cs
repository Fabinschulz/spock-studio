using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Engineering;
using Mariliz.Application.Management;
using Mariliz.Application.Mcp;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Core.Discovery;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Application.Tests;

public sealed class McpServerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private string Authorize() => Workspace().Authorize(_project.Root).Project!.ProjectId;

    private McpServer Server(string? workingDirectory = null, IReadOnlySet<string>? scope = null) => new("1.2.3", new ProjectContextTools(
        Workspace, new TestFilesFactory(), new FakeClock { UtcNow = Now }, new FileTechnologyProfileStore(_project.DataDir),
        new FileDecisionStore(_project.DataDir), workingDirectory ?? _project.Root, scope ?? McpScope.For(_project.Root)).Tools());

    private static JsonObject Send(McpServer server, string method, JsonObject? parameters = null) =>
        Raw(server, new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 7, ["method"] = method, ["params"] = parameters }.ToJsonString())!;

    private static JsonObject? Raw(McpServer server, string line) => server.HandleAsync(line).GetAwaiter().GetResult();

    private static (string Text, bool Failed) Call(McpServer server, string tool, JsonObject? arguments = null)
    {
        var result = Send(server, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments ?? [] })["result"]!;
        return (result["content"]![0]!["text"]!.GetValue<string>(), result["isError"]!.GetValue<bool>());
    }

    private void Decide(string projectId, string id, string title, string decision, int daysAgo) =>
        new FileDecisionStore(_project.DataDir).Save(new ManagementDecision(
            id, projectId, Now.AddDays(-daysAgo), title, decision, "Motivo registrado.", [], [], []));

    private void Profile(string projectId, string command) =>
        new FileTechnologyProfileStore(_project.DataDir).Write(new TechnologyProfile(projectId, Now,
            [new ProfileTechnology("dotnet", ".NET", "backend", ["10.0"], ["src"], Confidence.High, [])],
            [new ProfileTool("test", "dotnet", command)], ["claude-code"], [], [], []));

    [Fact]
    public void Responde_na_versao_que_a_ide_pede_e_cai_na_mais_recente_quando_nao_conhece()
    {
        var server = Server();

        var known = Send(server, "initialize", new JsonObject { ["protocolVersion"] = "2025-03-26" })["result"]!;
        var unknown = Send(server, "initialize", new JsonObject { ["protocolVersion"] = "1999-01-01" })["result"]!;

        Assert.Equal("2025-03-26", known["protocolVersion"]!.GetValue<string>());
        Assert.Equal(McpServer.LatestProtocol, unknown["protocolVersion"]!.GetValue<string>());
        Assert.Equal("spock-studio", known["serverInfo"]!["name"]!.GetValue<string>());
        Assert.Equal("1.2.3", known["serverInfo"]!["version"]!.GetValue<string>());
        Assert.NotNull(known["capabilities"]!["tools"]);
    }

    [Fact]
    public void Notificacao_nao_tem_resposta_e_o_resto_segue_o_json_rpc()
    {
        var server = Server();

        Assert.Null(Raw(server, """{"jsonrpc":"2.0","method":"notifications/initialized"}"""));
        Assert.Equal(-32700, Raw(server, "{quebrado")!["error"]!["code"]!.GetValue<int>());
        Assert.Equal(-32601, Send(server, "resources/list")["error"]!["code"]!.GetValue<int>());
        Assert.Equal(-32602, Send(server, "tools/call", new JsonObject { ["name"] = "apagar_tudo" })["error"]!["code"]!.GetValue<int>());
        Assert.NotNull(Raw(server, "\uFEFF" + """{"jsonrpc":"2.0","id":1,"method":"ping"}""")!["result"]);
    }

    // Revalidação 2026-11-15: o PowerShell abre a entrada com o BOM numa linha sozinha, que virava erro de JSON.
    [Fact]
    public async Task Linha_so_com_o_bom_e_ignorada_como_linha_vazia()
    {
        var output = new StringWriter();

        await Server().RunAsync(new StringReader("﻿\n" + """{"jsonrpc":"2.0","id":7,"method":"ping"}""" + "\n"), output, CancellationToken.None);

        var lines = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal(7, JsonNode.Parse(lines[0])!["id"]!.GetValue<int>());
    }

    [Fact]
    public void Oferece_so_ferramentas_de_leitura()
    {
        var tools = Send(Server(), "tools/list")["result"]!["tools"]!.AsArray();

        Assert.Equal(["list_projects", "project_briefing", "decisions", "search_project"], tools.Select(tool => tool!["name"]!.GetValue<string>()));
        Assert.All(tools, tool => Assert.True(tool!["annotations"]!["readOnlyHint"]!.GetValue<bool>()));
    }

    [Fact]
    public void Sem_projeto_aberto_no_Spock_diz_como_liberar_o_contexto()
    {
        var server = Server();

        Assert.Contains("Nenhum dos projetos desta conexão está aberto no Spock", Call(server, "list_projects").Text);
        var (text, failed) = Call(server, "project_briefing");
        Assert.True(failed);
        Assert.Contains("abra a pasta no Spock", text);
    }

    [Fact]
    public void A_pasta_atual_escolhe_o_projeto_e_um_aberto_depois_de_iniciar_ja_aparece()
    {
        var subfolder = Directory.CreateDirectory(Path.Combine(_project.Root, "src", "Api")).FullName;
        var server = Server(subfolder);
        Assert.Contains("Nenhum dos projetos", Call(server, "list_projects").Text);

        var projectId = Authorize();

        var listed = Call(server, "list_projects").Text;
        Assert.Contains($"id {projectId}", listed);
        Assert.Contains("(pasta atual)", listed);
        Assert.Contains($"Id no Spock: {projectId}", Call(server, "project_briefing").Text);
    }

    [Fact]
    public void Pasta_fora_dos_projetos_abertos_nao_vira_projeto()
    {
        var projectId = Authorize();
        // Com dois projetos no escopo a pasta é quem escolheria; com um só, o projeto do escopo vale fora de qualquer pasta.
        var scope = McpScope.Of(projectId, "p-ffffffffffff");
        var outside = Directory.CreateTempSubdirectory("mariliz-fora-").FullName;
        try
        {
            var (text, failed) = Call(Server(outside, scope), "project_briefing");

            Assert.True(failed);
            Assert.Contains("não é de um projeto disponível para esta conexão", text);
            Assert.DoesNotContain("(pasta atual)", Call(Server(outside, scope), "list_projects").Text);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Projeto_pelo_nome_ou_pelo_id_e_nome_desconhecido_e_erro_da_ferramenta()
    {
        var projectId = Authorize();
        var server = Server(Path.GetTempPath());
        var name = Path.GetFileName(_project.Root);

        Assert.False(Call(server, "project_briefing", new JsonObject { ["project"] = name.ToUpperInvariant() }).Failed);
        Assert.False(Call(server, "project_briefing", new JsonObject { ["project"] = projectId }).Failed);
        var (text, failed) = Call(server, "decisions", new JsonObject { ["project"] = "outro" });
        Assert.True(failed);
        Assert.Contains("list_projects", text);
    }

    [Fact]
    public async Task Resumo_traz_tecnologias_comandos_harness_e_as_decisoes_mais_recentes()
    {
        SpecificationFixture.Install(_project);
        var journey = new Journey(_project);
        await journey.ApplyAsync(await journey.PlanAsync());
        var projectId = Authorize();
        Profile(projectId, "dotnet test");
        for (var day = 0; day < 7; day++) Decide(projectId, $"dec-{day:x12}", $"Decisão {day}", $"Fazer {day}", day);

        var (text, failed) = Call(Server(), "project_briefing", new JsonObject { ["max_chars"] = ProjectContextTools.MaxChars });

        Assert.False(failed);
        Assert.Contains("- .NET 10.0 (backend) em src, confiança alta", text);
        Assert.Contains("- test: dotnet, comando `dotnet test`", text);
        Assert.Contains("## Harness\nAplicado", text);
        Assert.Contains("íntegros", text);
        Assert.Contains("## Decisões (5 mais recentes de 7; o resto em decisions)", text);
        Assert.Contains("Decisão 0", text);
        Assert.DoesNotContain("Decisão 5", text);
    }

    [Fact]
    public void Resumo_cabe_no_limite_pedido_e_avisa_o_corte()
    {
        var projectId = Authorize();
        for (var day = 0; day < 5; day++) Decide(projectId, $"dec-{day:x12}", $"Decisão {day}", new string('x', 600), day);

        var (text, _) = Call(Server(), "project_briefing", new JsonObject { ["max_chars"] = ProjectContextTools.MinChars });

        Assert.Contains("[Cortado para caber em 1000 caracteres.", text);
        Assert.True(text.Length < ProjectContextTools.MinChars + 150, $"{text.Length} caracteres");
        Assert.Contains("Ainda não aplicado", text);
    }

    [Fact]
    public void Decisoes_filtram_por_texto_e_respeitam_o_limite()
    {
        var projectId = Authorize();
        Decide(projectId, "dec-000000000001", "Banco", "Usar PostgreSQL", 3);
        Decide(projectId, "dec-000000000002", "Fila", "Usar RabbitMQ", 2);
        Decide(projectId, "dec-000000000003", "Banco de leitura", "Réplica do PostgreSQL", 1);
        var server = Server();

        var filtered = Call(server, "decisions", new JsonObject { ["query"] = "postgresql" }).Text;
        var limited = Call(server, "decisions", new JsonObject { ["limit"] = 1 }).Text;
        var none = Call(server, "decisions", new JsonObject { ["query"] = "Kafka" });

        Assert.Contains("Usar PostgreSQL", filtered);
        Assert.Contains("Réplica do PostgreSQL", filtered);
        Assert.DoesNotContain("RabbitMQ", filtered);
        Assert.Contains("Banco de leitura", limited);
        Assert.DoesNotContain("Fila", limited);
        Assert.False(none.Failed);
        Assert.Contains("fala de 'Kafka'", none.Text);
    }

    [Theory]
    [InlineData("max_chars", "\"muito\"", "precisa ser um número inteiro")]
    [InlineData("max_chars", "50", "vai de 1000 a 20000")]
    [InlineData("project", "42", "precisa ser um texto")]
    public void Argumento_invalido_volta_para_o_modelo_como_erro_da_ferramenta(string name, string json, string message)
    {
        Authorize();

        var (text, failed) = Call(Server(), "project_briefing", new JsonObject { [name] = JsonNode.Parse(json) });

        Assert.True(failed);
        Assert.Contains(message, text);
    }

    [Fact]
    public void Linha_que_parece_segredo_nunca_sai_para_a_ide()
    {
        var projectId = Authorize();
        Profile(projectId, "deploy --token ghp_abcdefghijklmnopqrstuvwxyz0123456789");

        var text = Call(Server(), "project_briefing").Text;

        Assert.DoesNotContain("ghp_abcdef", text);
        Assert.Contains("[linha omitida", text);
    }

    [Fact]
    public void O_aplicativo_mostra_como_ligar_o_servidor_na_ide()
    {
        var session = new EngineSession("test", ide: new IdeConnection(@"C:\Spock\mariliz-runtime.exe"));

        var response = session.Handle(new JsonObject
        {
            ["v"] = 1, ["kind"] = "request", ["id"] = "t", ["method"] = ProtocolMethods.IdeConnection, ["params"] = new JsonObject()
        }.ToJsonString()).Response;
        var result = response["result"]!;

        Assert.Equal(@"C:\Spock\mariliz-runtime.exe", result["command"]!.GetValue<string>());
        Assert.Equal(["mcp"], result["args"]!.AsArray().Select(arg => arg!.GetValue<string>()));
        Assert.Equal(4, result["tools"]!.AsArray().Count);

        var pretty = result.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).ReplaceLineEndings("\n");
        var path = ContractFiles.Path_("fixtures", "ide", "connection.json");
        if (Environment.GetEnvironmentVariable("MARILIZ_UPDATE_GOLDEN") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, pretty + "\n");
            return;
        }

        Assert.True(pretty == File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(),
            "ide.connection: o resultado mudou. Se foi deliberado, regenere com MARILIZ_UPDATE_GOLDEN=1 e ajuste o Zod da interface.");
    }
}
