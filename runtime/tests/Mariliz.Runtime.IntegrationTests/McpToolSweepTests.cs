using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Runtime.IntegrationTests;

// Varredura ferramenta por ferramenta do servidor MCP real (processo, JSON-RPC): cada ferramenta anunciada em tools/list recebe argumentos
// ausentes, de tipo errado (a partir do próprio esquema), hostis (caminho para fora do projeto, texto grande, caracteres de controle) e
// o servidor tem de responder sempre com um resultado (isError quando a ferramenta recusa) ou um erro JSON-RPC, nunca com a pilha no
// texto, nunca travando, e continuar vivo. Também cobre o envelope: método desconhecido, tools/call sem nome, nome de ferramenta que não
// existe, arguments que não é objeto. Sem modelo, rede, nuvem nem credencial. Um achado é um bug: entrada inválida devia virar recusa.
public sealed partial class McpToolSweepTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly List<McpClient> _clients = [];
    private readonly List<string> _folders = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients) await client.DisposeAsync();
        foreach (var folder in _folders)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
                    break;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(200);
                }
            }
        }
    }

    private string NewFolder(string prefix)
    {
        var folder = Directory.CreateTempSubdirectory(prefix).FullName;
        _folders.Add(folder);
        return folder;
    }

    private const string StableId = "spk-01M7GTEK00ABCDEFGHJKMNPQRS";

    // O servidor MCP só sobe numa pasta de projeto que tenha o spock.yaml com o id (o Harness grava), e a pasta de dados autoriza a pasta.
    private string NewProject(string data)
    {
        var root = NewFolder("spock-mcp-varredura-projeto-");
        File.WriteAllText(Path.Combine(root, "spock.yaml"),
            $"schema: 1\nproject:\n  id: {StableId}\n  name: loja\nimplementationModel: mariliz\naiEnvironment: claude-code\n", new UTF8Encoding(false));
        Directory.CreateDirectory(Path.Combine(root, "backend", "src", "App"));
        File.WriteAllText(Path.Combine(root, "backend", "App.sln"), "Microsoft Visual Studio Solution File, Format Version 12.00\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "backend", "src", "App", "App.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "README.md"), "# App\n\nUm serviço de pedidos.\n", new UTF8Encoding(false));
        _ = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(data)).Authorize(root);
        return root;
    }

    [GeneratedRegex(@"(^|\n)\s+at \w|--- End of|System\.\w+\.|Mariliz\.\w+\.|\.cs:line \d+")]
    private static partial Regex StackTrace();

    // Valores hostis para uma propriedade, de acordo com o tipo que o esquema anuncia.
    private static IEnumerable<(string Name, JsonNode? Value)> Hostile(string type)
    {
        yield return ("nulo", null);
        yield return ("inteiro", JsonValue.Create(12345));
        yield return ("lista", new JsonArray());
        yield return ("objeto", new JsonObject());
        yield return ("booleano", JsonValue.Create(true));
        if (type is "string" or "")
        {
            yield return ("caminho para fora", JsonValue.Create("../../../Windows/System32"));
            yield return ("caminho absoluto", JsonValue.Create(Path.GetPathRoot(Path.GetTempPath())));
            yield return ("controle", JsonValue.Create("\u0000\u001b[31m\r\nx" + (char)0x2028));
            yield return ("texto grande", JsonValue.Create(new string('A', 300_000)));
            yield return ("vazio", JsonValue.Create(""));
        }
    }

    [Fact]
    public async Task Toda_ferramenta_responde_com_recusa_estruturada_a_argumentos_hostis_e_o_servidor_continua_vivo()
    {
        var data = NewFolder("spock-mcp-varredura-dados-");
        var project = NewProject(data);
        var client = McpClient.Start(data, "--project-dir", project);
        _clients.Add(client);
        await client.InitializeAsync("varredura");
        var tools = (await client.CallAsync("tools/list"))["result"]!["tools"]!.AsArray().Select(tool => tool!.AsObject()).ToList();
        Assert.NotEmpty(tools);

        var findings = new List<string>();
        var calls = 0;
        foreach (var tool in tools)
        {
            var name = tool["name"]!.GetValue<string>();
            var properties = tool["inputSchema"]?["properties"] as JsonObject ?? [];
            var requests = new List<(string Variant, JsonNode? Arguments)> { ("sem argumentos", new JsonObject()), ("arguments nulo", null), ("arguments lista", new JsonArray()) };
            foreach (var (property, schema) in properties)
            {
                var type = schema?["type"]?.GetValue<string>() ?? "";
                foreach (var (variant, value) in Hostile(type))
                {
                    var arguments = new JsonObject();
                    foreach (var other in properties) arguments[other.Key] = other.Key == property ? value?.DeepClone() : (other.Value?["type"]?.GetValue<string>() == "string" ? "x" : null);
                    requests.Add(($"{property}: {variant}", arguments));
                }
            }

            foreach (var (variant, arguments) in requests)
            {
                calls++;
                try
                {
                    var response = await client.CallAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments?.DeepClone() });
                    Inspect(name, variant, response, findings);
                }
                catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
                {
                    findings.Add($"{name} [{variant}]: sem resposta ({ex.GetType().Name})");
                    break;
                }
            }
        }

        // O envelope: o que um cliente mal-educado pode mandar.
        var envelope = new (string Name, string Method, JsonObject? Params)[]
        {
            ("método desconhecido", "metodo/que/nao/existe", null),
            ("tools/call sem nome", "tools/call", new JsonObject()),
            ("nome que não é texto", "tools/call", new JsonObject { ["name"] = 123, ["arguments"] = new JsonObject() }),
            ("ferramenta inexistente", "tools/call", new JsonObject { ["name"] = "spock_nao_existe", ["arguments"] = new JsonObject() }),
            ("nome enorme", "tools/call", new JsonObject { ["name"] = new string('x', 100_000), ["arguments"] = new JsonObject() }),
            ("params não é objeto", "tools/call", null),
        };
        foreach (var (variant, method, parameters) in envelope)
        {
            calls++;
            try
            {
                Inspect("(envelope)", variant, await client.CallAsync(method, parameters), findings);
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
            {
                findings.Add($"(envelope) [{variant}]: sem resposta ({ex.GetType().Name})");
            }
        }

        var report = new StringBuilder()
            .AppendLine($"Varredura MCP: {tools.Count} ferramentas ({string.Join(", ", tools.Select(tool => tool["name"]!.GetValue<string>()))}); {calls} pedidos hostis.")
            .AppendLine($"Achados: {findings.Count}");
        foreach (var finding in findings.Distinct().Take(60)) report.AppendLine("  - " + finding);
        output.WriteLine(report.ToString());

        // Uma varredura que não exercita nada passa em silêncio: piso de cobertura.
        Assert.True(tools.Count >= 8 && calls >= 150, $"Cobertura baixa demais: {tools.Count} ferramentas, {calls} pedidos.\n{report}");
        Assert.True(findings.Count == 0, report.ToString());
        // A entrada fecha e o servidor sai limpo, depois de tudo.
        Assert.Equal(0, await client.CloseAsync());
    }

    private static void Inspect(string tool, string variant, JsonObject response, List<string> findings)
    {
        var error = response["error"] as JsonObject;
        var result = response["result"] as JsonObject;
        if (error is null && result is null) findings.Add($"{tool} [{variant}]: resposta sem result nem error");
        if (error is not null && error["code"] is null) findings.Add($"{tool} [{variant}]: erro JSON-RPC sem código");
        var text = error?["message"]?.GetValue<string>() ?? string.Concat((result?["content"] as JsonArray ?? []).Select(item => item?["text"]?.GetValue<string>() ?? ""));
        if (StackTrace().IsMatch(text)) findings.Add($"{tool} [{variant}]: pilha no texto: {text[..Math.Min(120, text.Length)]}");
        if (text.Contains("Falha interna", StringComparison.Ordinal)) findings.Add($"{tool} [{variant}]: falha interna: {text[..Math.Min(120, text.Length)]}");
    }
}
