using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

// O servidor MCP é o próprio executável do motor com o argumento "mcp": a IDE o inicia e conversa pelo stdio.
public sealed class IdeConnection(string? enginePath)
{
    public const string ServerArgument = "mcp";

    public JsonObject Describe() => new()
    {
        ["command"] = enginePath,
        ["args"] = new JsonArray(ServerArgument),
        ["tools"] = new JsonArray(ProjectContextTools.Catalog
            .Select(tool => (JsonNode)new JsonObject { ["name"] = tool.Name, ["title"] = tool.Title, ["description"] = tool.Description })
            .ToArray())
    };
}
