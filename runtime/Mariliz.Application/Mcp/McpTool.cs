using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

public sealed record McpToolInfo(string Name, string Title, string Description, JsonObject InputSchema);

public sealed record McpTool(McpToolInfo Info, Func<JsonObject, string> Call);

// O erro volta para o modelo como resultado da ferramenta, não como falha do protocolo: ele lê e corrige o pedido.
public sealed class McpToolException(string message) : Exception(message);
