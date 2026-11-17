using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Core.Policy;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sdk = ModelContextProtocol.Server;

namespace Mariliz.Application.Mcp;

// O protocolo é do SDK oficial (ADR-0009); o que é do Spock fica aqui: as ferramentas, todas de leitura, a redação de
// segredos e o nome do cliente para a auditoria. O servidor próprio que veio antes está registrado na ADR para restauração.
public sealed partial class McpServer(string version, IReadOnlyList<McpTool> tools)
{
    public const string ServerName = "spock-studio";

    private const string Instructions =
        "Contexto do Spock Studio sobre os projetos que a pessoa abriu nele: tecnologias, comandos, estado do Harness, decisões registradas " +
        "e busca no índice de conhecimento. Só leitura. Comece por project_briefing e use search_project para achar o código e os documentos " +
        "de um assunto; sem 'project', vale o projeto da pasta atual. Quando o projeto liberou conexões cloud para agentes, cloud_connections e as " +
        "ferramentas aws_*, azure_* e gcp_* leem a infraestrutura, também só leitura.";

    public McpServerOptions Options() => new()
    {
        ServerInfo = new Implementation { Name = ServerName, Title = "Spock Studio", Version = version },
        ServerInstructions = Instructions,
        Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } },
        Handlers = new McpServerHandlers
        {
            ListToolsHandler = (_, _) => ValueTask.FromResult(new ListToolsResult { Tools = [.. tools.Select(Describe)] }),
            CallToolHandler = (request, ct) => CallAsync(request.Params, ClientName(request.Server.ClientInfo?.Name), ct)
        }
    };

    // O modo mcp do motor: a IDE inicia o processo e conversa pelo stdin e stdout.
    public Task RunStdioAsync(CancellationToken ct) => RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), ct);

    public async Task RunAsync(Stream input, Stream output, CancellationToken ct)
    {
        await using var transport = new StreamServerTransport(new LeadingBomSkipStream(input), output, ServerName);
        await using var server = Sdk.McpServer.Create(transport, Options());
        await server.RunAsync(ct);
    }

    private static Tool Describe(McpTool tool) => new()
    {
        Name = tool.Info.Name,
        Title = tool.Info.Title,
        Description = tool.Info.Description,
        InputSchema = JsonSerializer.SerializeToElement(tool.Info.InputSchema),
        Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = tool.Info.OpenWorld }
    };

    private async ValueTask<CallToolResult> CallAsync(CallToolRequestParams? parameters, string? client, CancellationToken ct)
    {
        var tool = tools.FirstOrDefault(item => item.Info.Name == parameters?.Name)
            ?? throw new McpProtocolException($"Ferramenta desconhecida: {parameters?.Name}.", McpErrorCode.InvalidParams);

        string text;
        var failed = false;
        try
        {
            text = await tool.Call(new McpCall(Arguments(parameters!), client, ct));
        }
        catch (Exception ex) when (ex is McpToolException or EngineException)
        {
            text = ex.Message;
            failed = true;
        }
        // Uma falha de leitura não pode derrubar o servidor: a IDE perderia o Spock até reiniciar.
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            text = $"Falha inesperada ao ler o contexto ({ex.GetType().Name}).";
            failed = true;
        }

        return new CallToolResult { Content = [new TextContentBlock { Text = SecretScanner.RedactLines(text) }], IsError = failed };
    }

    private static JsonObject Arguments(CallToolRequestParams parameters)
    {
        var arguments = new JsonObject();
        foreach (var (name, value) in parameters.Arguments ?? new Dictionary<string, JsonElement>())
            arguments[name] = JsonNode.Parse(value.GetRawText());
        return arguments;
    }

    // Vai para a trilha de auditoria: só o que parece um nome de programa, curto. É o que o cliente diz de si, não uma
    // identidade conferida.
    private static string? ClientName(string? name)
    {
        var trimmed = name?.Trim();
        return trimmed is { Length: > 0 and <= 60 } && ClientPattern().IsMatch(trimmed) ? trimmed : null;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} ._\-]*$")]
    private static partial Regex ClientPattern();
}
