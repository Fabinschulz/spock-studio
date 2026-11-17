using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

/// <param name="OpenWorld">Fala com sistema externo (a cloud); o contexto local do Spock não.</param>
/// <param name="OutputSchema">Schema do JSON que a ferramenta devolve em structuredContent, quando devolve.</param>
public sealed record McpToolInfo(string Name, string Title, string Description, JsonObject InputSchema, bool OpenWorld = false,
    JsonObject? OutputSchema = null);

/// <param name="Client">O nome que a IDE deu de si no initialize; é o que ela diz, não uma identidade conferida.</param>
public sealed record McpCall(JsonObject Arguments, string? Client, CancellationToken Ct);

/// <param name="Structured">A mesma resposta em JSON (structuredContent). O texto vai sempre junto: há cliente que descarta a
/// resposta que vem só estruturada.</param>
public sealed record McpReply(string Text, JsonObject? Structured = null);

public sealed record McpTool(McpToolInfo Info, Func<McpCall, Task<string>> Call)
{
    public McpTool(McpToolInfo info, Func<JsonObject, string> call) : this(info, request => Task.FromResult(call(request.Arguments)))
    {
    }

    // Nulo nas ferramentas que só respondem em texto.
    public Func<McpCall, Task<McpReply>>? Reply { get; private init; }

    public static McpTool Structured(McpToolInfo info, Func<McpCall, Task<McpReply>> reply) =>
        new(info, async call => (await reply(call)).Text) { Reply = reply };
}

// O erro volta para o modelo como resultado da ferramenta, não como falha do protocolo: ele lê e corrige o pedido.
public sealed class McpToolException(string message) : Exception(message);
