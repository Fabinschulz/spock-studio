using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

// O servidor MCP é o próprio executável do motor com o argumento "mcp": a IDE o inicia e conversa pelo stdio.
/// <param name="launcherAvailable">Se o lançador spock (DD-6) está instalado: com ele, a IDE chama "spock" e não o caminho do
/// motor nesta máquina.</param>
public sealed class IdeConnection(string? enginePath, Func<bool>? launcherAvailable = null)
{
    public const string ServerArgument = "mcp";
    public const string Launcher = "spock";

    public JsonObject Describe() => new()
    {
        ["command"] = enginePath,
        ["args"] = new JsonArray(ServerArgument),
        // Repetido por projeto, depois de "mcp": a conexão passa a ler só esses.
        ["projectArgument"] = McpOptions.ProjectArgument,
        // O comando a usar no lugar de command quando o lançador existe; nulo quando não.
        ["launcher"] = launcherAvailable?.Invoke() == true ? Launcher : null,
        ["tools"] = new JsonArray(ProjectContextTools.Catalog
            .Select(tool => (JsonNode)new JsonObject { ["name"] = tool.Name, ["title"] = tool.Title, ["description"] = tool.Description })
            .ToArray())
    };
}
