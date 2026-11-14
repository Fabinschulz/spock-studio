using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Mcp;

// JSON-RPC 2.0 por linhas no stdio, como as IDEs iniciam um servidor MCP local. Só ferramentas, todas de leitura.
public sealed class McpServer(string version, IReadOnlyList<McpTool> tools)
{
    public const string LatestProtocol = "2025-06-18";
    private static readonly string[] SupportedProtocols = [LatestProtocol, "2025-03-26", "2024-11-05"];

    private const int ParseError = -32700;
    private const int InvalidRequest = -32600;
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;

    private const string Instructions =
        "Contexto do Spock Studio sobre os projetos que a pessoa abriu nele: tecnologias, comandos, estado do Harness e decisões registradas. " +
        "Só leitura. Comece por project_briefing; sem 'project', vale o projeto da pasta atual.";

    public async Task RunAsync(TextReader input, TextWriter output, CancellationToken ct)
    {
        while (await input.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (Handle(line) is not { } reply) continue;
            await output.WriteLineAsync(reply.ToJsonString());
            await output.FlushAsync(ct);
        }
    }

    public JsonObject? Handle(string line)
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
            "tools/call" => Call(id, parameters),
            _ => Error(id, MethodNotFound, $"Método desconhecido: {method}.")
        };
    }

    private JsonObject Initialize(JsonObject parameters)
    {
        var requested = parameters["protocolVersion"] is JsonValue value && value.TryGetValue<string>(out var asked) ? asked : null;
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

    private JsonObject Call(JsonNode id, JsonObject parameters)
    {
        var name = parameters["name"] is JsonValue value && value.TryGetValue<string>(out var asked) ? asked : null;
        var tool = tools.FirstOrDefault(item => item.Info.Name == name);
        if (tool is null) return Error(id, InvalidParams, $"Ferramenta desconhecida: {name}.");

        string text;
        var failed = false;
        try
        {
            text = tool.Call(parameters["arguments"] as JsonObject ?? []);
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

    private static JsonObject Ok(JsonNode id, JsonObject result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };
}
