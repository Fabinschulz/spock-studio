using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Só projetos que a pessoa autorizou no Spock e, com escopo, só os da conexão: a pasta atual da IDE escolhe entre eles, nunca autoriza outro.
public sealed class McpProjectResolver(WorkspaceAuthorizer workspace, string workingDirectory, IReadOnlySet<string>? scope = null)
{
    public IReadOnlyList<AuthorizedProject> Projects() =>
        workspace.Projects().Where(project => scope is null || scope.Contains(project.ProjectId)).ToList();

    public AuthorizedProject? Current() =>
        Projects()
            .Select(project => (Project: project, Root: workspace.TryGetRoot(project.ProjectId, out var root) ? root : ""))
            .Where(item => item.Root.Length > 0 && Contains(item.Root, workingDirectory))
            .OrderByDescending(item => item.Root.Length)
            .Select(item => item.Project)
            .FirstOrDefault();

    public AuthorizedProject Resolve(string? project)
    {
        if (project is null)
            return Current() ?? throw new McpToolException(
                $"A pasta atual não é de um projeto {Available}. Informe 'project' com um nome de list_projects, ou abra a pasta no Spock primeiro.");

        // Fora do escopo responde igual a inexistente: a conexão não fica sabendo de projetos que não pode ler.
        var matches = Projects()
            .Where(item => item.ProjectId == project || string.Equals(item.DisplayName, project, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new McpToolException($"'{project}' não é um projeto {Available}. Veja os disponíveis em list_projects."),
            _ => throw new McpToolException($"Há mais de um projeto chamado '{project}'. Use o id de list_projects.")
        };
    }

    private string Available => scope is null ? "aberto no Spock" : "disponível para esta conexão";

    private static bool Contains(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
