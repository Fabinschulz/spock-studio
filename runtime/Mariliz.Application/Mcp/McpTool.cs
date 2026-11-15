using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

public sealed record McpToolInfo(string Name, string Title, string Description, JsonObject InputSchema);

/// <param name="Client">O nome que a IDE deu de si no initialize; é o que ela diz, não uma identidade conferida.</param>
public sealed record McpCall(JsonObject Arguments, string? Client, CancellationToken Ct);

public sealed record McpTool(McpToolInfo Info, Func<McpCall, Task<string>> Call)
{
    public McpTool(McpToolInfo info, Func<JsonObject, string> call) : this(info, request => Task.FromResult(call(request.Arguments)))
    {
    }
}

// O erro volta para o modelo como resultado da ferramenta, não como falha do protocolo: ele lê e corrige o pedido.
public sealed class McpToolException(string message) : Exception(message);
