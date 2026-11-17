using Mariliz.Core.Text;

namespace Mariliz.Application.Mcp;

public sealed class McpOptionsException(string message) : Exception(message);

/// <param name="Scope">Os projetos que este cliente pode ler, por id da cópia (p-) ou id estável (spk-); nunca vazio depois de resolvido.</param>
/// <param name="ProjectDirectory">A pasta do projeto na IDE (--project-dir); o id estável sai do mariliz.yaml dela.</param>
public sealed record McpOptions(IReadOnlySet<string> Scope, string? ProjectDirectory = null)
{
    public const string ProjectArgument = "--project";
    public const string ProjectDirectoryArgument = "--project-dir";
    public const int MaxProjects = 20;

    // Sem escopo, ou com um argumento que não se entende, o servidor não sobe: não existe modo "todos os projetos" para a IDE.
    public static McpOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            throw new McpOptionsException(
                $"O modo MCP precisa de {ProjectArgument} <id do projeto> ou {ProjectDirectoryArgument} <pasta do projeto>: sem eles, nenhum projeto é exposto à IDE. " +
                "Copie o comando pronto em Agentes, na conexão com a IDE, com o projeto aberto no Spock.");
        var scope = new HashSet<string>(StringComparer.Ordinal);
        string? directory = null;
        for (var index = 0; index < args.Count; index++)
        {
            var value = index + 1 < args.Count ? args[index + 1] : null;
            switch (args[index])
            {
                case ProjectArgument:
                    if (!Ids.IsProjectId(value) && !StableProjectId.IsValid(value))
                        throw new McpOptionsException(
                            $"{ProjectArgument} precisa de um id do Spock: o da cópia (p- e 12 caracteres, como em list_projects) ou o do projeto ({StableProjectId.Prefix}, como em project.id do mariliz.yaml).");
                    scope.Add(value!);
                    break;
                case ProjectDirectoryArgument:
                    if (string.IsNullOrWhiteSpace(value) || directory is not null)
                        throw new McpOptionsException($"{ProjectDirectoryArgument} precisa de uma pasta, uma vez só.");
                    directory = value;
                    break;
                default:
                    throw new McpOptionsException(
                        $"Argumento desconhecido no modo MCP. Use {ProjectArgument} <id do projeto> (uma vez por projeto) ou {ProjectDirectoryArgument} <pasta do projeto>.");
            }
            index++;
        }
        if (scope.Count > MaxProjects)
            throw new McpOptionsException($"No máximo {MaxProjects} projetos por conexão.");
        return new McpOptions(scope, directory);
    }

    // --project-dir vira o id estável do mariliz.yaml mais próximo, subindo a partir da pasta. Sem id, recusa: o vínculo pela
    // pasta só vale para um projeto que tem identidade própria, e nunca vira "o que estiver por perto".
    public McpOptions WithDirectoryResolved(Func<string, string?> readDeclaration)
    {
        if (ProjectDirectory is null) return this;
        var full = Path.GetFullPath(ProjectDirectory);
        for (var current = new DirectoryInfo(full); current is not null; current = current.Parent)
        {
            var text = readDeclaration(current.FullName);
            if (text is null) continue;
            var id = Harness.DeclaredConfig.StableIdIn(text) ?? throw new McpOptionsException(
                $"O mariliz.yaml de {current.FullName} não tem project.id. Abra o projeto no Spock, planeje e aplique o Harness para gravar o id, e reinicie a conexão.");
            return new McpOptions(new HashSet<string>(Scope, StringComparer.Ordinal) { id }, full);
        }
        throw new McpOptionsException(
            $"Nenhum mariliz.yaml em {full} nem nas pastas acima. Abra o projeto no Spock, planeje e aplique o Harness para gravar o id, e reinicie a conexão.");
    }
}
