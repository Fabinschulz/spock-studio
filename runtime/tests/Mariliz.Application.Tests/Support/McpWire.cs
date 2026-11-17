using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Mcp;

namespace Mariliz.Application.Tests.Support;

// Conversa com o servidor pelo fio, como uma IDE: JSON-RPC uma mensagem por linha, sobre o transporte de streams do SDK.
internal static class McpWire
{
    public const string ClientProtocol = "2025-06-18";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    public static string Initialize(int id = 0, string protocol = ClientProtocol, string client = "teste") => new JsonObject
    {
        ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = "initialize",
        ["params"] = new JsonObject
        {
            ["protocolVersion"] = protocol, ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = client, ["version"] = "1.0" }
        }
    }.ToJsonString();

    public const string Initialized = """{"jsonrpc":"2.0","method":"notifications/initialized"}""";

    public static string Request(int id, string method, JsonObject? parameters = null) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters ?? new JsonObject() }.ToJsonString();

    // Devolve as respostas por id, na ordem em que chegaram. Espera uma resposta para cada linha com id.
    public static IReadOnlyList<JsonObject> Exchange(McpServer server, params string[] lines) =>
        ExchangeAsync(server, lines).GetAwaiter().GetResult();

    private static async Task<IReadOnlyList<JsonObject>> ExchangeAsync(McpServer server, string[] lines)
    {
        var expected = lines.Count(line => line.Contains("\"id\"", StringComparison.Ordinal));
        var toServer = new Pipe();
        var fromServer = new Pipe();
        using var stop = new CancellationTokenSource(Wait);
        var running = server.RunAsync(toServer.Reader.AsStream(), fromServer.Writer.AsStream(), stop.Token);

        var input = toServer.Writer.AsStream();
        var reader = new StreamReader(fromServer.Reader.AsStream(), Encoding.UTF8);
        var replies = new List<JsonObject>();
        foreach (var line in lines)
        {
            await input.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"), stop.Token);
            await input.FlushAsync(stop.Token);
            // Uma de cada vez: a ordem das respostas fica a mesma dos pedidos.
            if (!line.Contains("\"id\"", StringComparison.Ordinal)) continue;
            while (await reader.ReadLineAsync(stop.Token) is { } reply)
            {
                if (JsonNode.Parse(reply) is JsonObject message && message.ContainsKey("id"))
                {
                    replies.Add(message);
                    break;
                }
            }
        }
        await toServer.Writer.CompleteAsync();
        await stop.CancelAsync();
        try { await running; } catch (OperationCanceledException) { }
        if (replies.Count != expected) throw new InvalidOperationException($"Esperava {expected} respostas e vieram {replies.Count}.");
        return replies;
    }

    // Uma sessão inteira de uma IDE para uma chamada de ferramenta.
    public static (string Text, bool Failed) Call(McpServer server, string tool, JsonObject? arguments = null)
    {
        var reply = Exchange(server, Initialize(), Initialized,
            Request(1, "tools/call", new JsonObject { ["name"] = tool, ["arguments"] = arguments ?? new JsonObject() }))[^1];
        var result = reply["result"] ?? throw new InvalidOperationException(reply.ToJsonString());
        return (result["content"]![0]!["text"]!.GetValue<string>(), result["isError"]?.GetValue<bool>() ?? false);
    }

    public static JsonArray Tools(McpServer server) =>
        Exchange(server, Initialize(), Initialized, Request(1, "tools/list"))[^1]["result"]!["tools"]!.AsArray();

    // Os testes das ferramentas mandam um pedido e leem a resposta crua; aqui ele vai depois do initialize, como numa IDE.
    public static Task<JsonObject?> HandleAsync(this McpServer server, string line) =>
        Task.FromResult<JsonObject?>(Exchange(server, Initialize(), Initialized, line)[^1]);
}
