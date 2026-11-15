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
        // Repetido por projeto, depois de "mcp": a conexão passa a ler só esses.
        ["projectArgument"] = McpOptions.ProjectArgument,
        ["tools"] = new JsonArray(ProjectContextTools.Catalog
            .Select(tool => (JsonNode)new JsonObject { ["name"] = tool.Name, ["title"] = tool.Title, ["description"] = tool.Description })
            .ToArray())
    };
}
