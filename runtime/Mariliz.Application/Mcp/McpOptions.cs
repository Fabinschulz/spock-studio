using Mariliz.Core.Text;

namespace Mariliz.Application.Mcp;

public sealed class McpOptionsException(string message) : Exception(message);

/// <param name="Scope">Os projetos que este cliente pode ler; nunca vazio.</param>
public sealed record McpOptions(IReadOnlySet<string> Scope)
{
    public const string ProjectArgument = "--project";
    public const int MaxProjects = 20;

    // Sem escopo, ou com um argumento que não se entende, o servidor não sobe: não existe modo "todos os projetos" para a IDE.
    public static McpOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            throw new McpOptionsException(
                $"O modo MCP precisa de {ProjectArgument} <id do projeto>: sem ele, nenhum projeto é exposto à IDE. " +
                "Copie o comando pronto em Agentes, na conexão com a IDE, com o projeto aberto no Spock.");
        var scope = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index++)
        {
            if (args[index] != ProjectArgument)
                throw new McpOptionsException($"Argumento desconhecido no modo MCP. Use {ProjectArgument} <id do projeto>, uma vez por projeto.");
            if (index + 1 >= args.Count || !Ids.IsProjectId(args[index + 1]))
                throw new McpOptionsException($"{ProjectArgument} precisa de um id de projeto do Spock (p- e 12 caracteres), como aparece em list_projects.");
            scope.Add(args[++index]);
        }
        if (scope.Count > MaxProjects)
            throw new McpOptionsException($"No máximo {MaxProjects} projetos por conexão.");
        return new McpOptions(scope);
    }
}
