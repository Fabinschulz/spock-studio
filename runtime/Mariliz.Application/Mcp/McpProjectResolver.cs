using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Só projetos que a pessoa autorizou no Spock e que estão no escopo da conexão: a pasta atual da IDE escolhe entre eles, nunca
// autoriza outro. O escopo é obrigatório; sem ele não há projeto nenhum.
public sealed class McpProjectResolver(WorkspaceAuthorizer workspace, string workingDirectory, IReadOnlySet<string> scope)
{
    public IReadOnlyList<AuthorizedProject> Projects() =>
        workspace.Projects().Where(project => scope.Contains(project.ProjectId)).ToList();

    public AuthorizedProject? Current() => Containing(Projects());

    // Um cliente sem pasta de projeto (o Claude Desktop) com escopo de um projeto só não tem o que escolher. Numa pasta de
    // outro projeto do Spock a resposta continua sendo erro, para a IDE nunca receber o contexto de um projeto por engano.
    private AuthorizedProject? Default()
    {
        if (Current() is { } current) return current;
        if (scope.Count != 1 || Containing(workspace.Projects()) is not null) return null;
        return Projects() is [var only] ? only : null;
    }

    private AuthorizedProject? Containing(IEnumerable<AuthorizedProject> projects) =>
        projects
            .Select(project => (Project: project, Root: workspace.TryGetRoot(project.ProjectId, out var root) ? root : ""))
            .Where(item => item.Root.Length > 0 && Contains(item.Root, workingDirectory))
            .OrderByDescending(item => item.Root.Length)
            .Select(item => item.Project)
            .FirstOrDefault();

    public AuthorizedProject Resolve(string? project)
    {
        if (project is null)
            return Default() ?? throw new McpToolException(
                $"A pasta atual não é de um projeto {Available}. Informe 'project' com um nome de list_projects, ou abra a pasta no Spock primeiro.");

        // Fora do escopo responde igual a inexistente: a conexão não fica sabendo de projetos que não pode ler. O nome da pasta
        // continua valendo porque era o nome exibido antes de o Spock ler o do manifesto, e configurações antigas o usam.
        var matches = Projects()
            .Where(item => item.ProjectId == project || string.Equals(item.DisplayName, project, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0)
            matches = Projects().Where(item => string.Equals(FolderOf(item), project, StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new McpToolException($"'{project}' não é um projeto {Available}. Veja os disponíveis em list_projects."),
            _ => throw new McpToolException($"Há mais de um projeto chamado '{project}'. Use o id de list_projects.")
        };
    }

    private string FolderOf(AuthorizedProject project) =>
        workspace.TryGetRoot(project.ProjectId, out var root) ? Path.GetFileName(root) : "";

    private const string Available = "disponível para esta conexão";

    private static bool Contains(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
