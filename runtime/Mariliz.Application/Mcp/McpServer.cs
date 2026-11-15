using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Mcp;

// JSON-RPC 2.0 por linhas no stdio, como as IDEs iniciam um servidor MCP local. Só ferramentas, todas de leitura.
public sealed partial class McpServer(string version, IReadOnlyList<McpTool> tools)
{
    public const string LatestProtocol = "2025-06-18";
    private static readonly string[] SupportedProtocols = [LatestProtocol, "2025-03-26", "2024-11-05"];

    private const int ParseError = -32700;
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;

    private const string Instructions =
        "Contexto do Spock Studio sobre os projetos que a pessoa abriu nele: tecnologias, comandos, estado do Harness, decisões registradas " +
        "e busca no índice de conhecimento. Só leitura. Comece por project_briefing e use search_project para achar o código e os documentos " +
        "de um assunto; sem 'project', vale o projeto da pasta atual.";

    private string? _client;

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        while (await input.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (await HandleAsync(line, ct) is not { } reply) continue;
            await output.WriteLineAsync(reply.ToJsonString());
            await output.FlushAsync(ct);
        }
    }

    public async Task<JsonObject?> HandleAsync(string line, CancellationToken ct = default)
    {
        JsonNode? parsed;
        try
        {
            // Cliente no Windows às vezes manda o BOM do UTF-8 na primeira linha.
            parsed = JsonNode.Parse(line.TrimStart('﻿'));
        }
        catch (JsonException)
        {
            return Error(null, ParseError, "JSON inválido.");
        }

        if (parsed is not JsonObject message) return Error(null, InvalidRequest, "Esperado um objeto JSON-RPC.");
        var id = message["id"]?.DeepClone();
        var method = message["method"] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;
        // Notificações (initialized, cancelled) não têm resposta.
        if (id is null) return null;
        if (method is null) return Error(id, InvalidRequest, "Falta 'method'.");
        var parameters = message["params"] as JsonObject ?? [];

        return method switch
        {
            "initialize" => Ok(id, Initialize(parameters)),
            "ping" => Ok(id, []),
            "tools/list" => Ok(id, new JsonObject { ["tools"] = new JsonArray(tools.Select(Describe).ToArray()) }),
            "tools/call" => await CallAsync(id, parameters, ct),
            _ => Error(id, MethodNotFound, $"Método desconhecido: {method}.")
        };
    }

    private JsonObject Initialize(JsonObject parameters)
    {
        var requested = parameters["protocolVersion"] is JsonValue value && value.TryGetValue<string>(out var asked) ? asked : null;
        _client = ClientName(parameters);
        return new JsonObject
        {
            ["protocolVersion"] = requested is not null && SupportedProtocols.Contains(requested) ? requested : LatestProtocol,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = "spock-studio", ["title"] = "Spock Studio", ["version"] = version },
            ["instructions"] = Instructions
        };
    }

    private static JsonNode Describe(McpTool tool) => new JsonObject
    {
        ["name"] = tool.Info.Name,
        ["title"] = tool.Info.Title,
        ["description"] = tool.Info.Description,
        ["inputSchema"] = tool.Info.InputSchema.DeepClone(),
        ["annotations"] = new JsonObject { ["readOnlyHint"] = true, ["openWorldHint"] = false }
    };

    private async Task<JsonObject> CallAsync(JsonNode id, JsonObject parameters, CancellationToken ct)
    {
        var name = parameters["name"] is JsonValue value && value.TryGetValue<string>(out var asked) ? asked : null;
        var tool = tools.FirstOrDefault(item => item.Info.Name == name);
        if (tool is null) return Error(id, InvalidParams, $"Ferramenta desconhecida: {name}.");

        string text;
        var failed = false;
        try
        {
            text = await tool.Call(new McpCall(parameters["arguments"] as JsonObject ?? [], _client, ct));
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

        return Ok(id, new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = SecretScanner.RedactLines(text) }),
            ["isError"] = failed
        });
    }

    // Vai para a trilha de auditoria: só o que parece um nome de programa, curto.
    private static string? ClientName(JsonObject parameters)
    {
        var name = parameters["clientInfo"] is JsonObject info && info["name"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : null;
        return name is { Length: > 0 and <= 60 } && ClientPattern().IsMatch(name) ? name : null;
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} ._\-]*$")]
    private static partial Regex ClientPattern();

    private static JsonObject Ok(JsonNode id, JsonObject result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
}
