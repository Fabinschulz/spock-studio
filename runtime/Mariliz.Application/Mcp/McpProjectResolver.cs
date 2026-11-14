using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Só projetos que a pessoa autorizou no Spock: a pasta atual da IDE escolhe entre eles, nunca autoriza outro.
public sealed class McpProjectResolver(WorkspaceAuthorizer workspace, string workingDirectory)
{
    public IReadOnlyList<AuthorizedProject> Projects() => workspace.Projects();

    public AuthorizedProject? Current() =>
        workspace.Projects()
            .Select(project => (Project: project, Root: workspace.TryGetRoot(project.ProjectId, out var root) ? root : ""))
            .Where(item => item.Root.Length > 0 && Contains(item.Root, workingDirectory))
            .OrderByDescending(item => item.Root.Length)
            .Select(item => item.Project)
            .FirstOrDefault();

    public AuthorizedProject Resolve(string? project)
    {
        if (project is null)
            return Current() ?? throw new McpToolException(
                "A pasta atual não é de um projeto aberto no Spock. Informe 'project' com um nome de list_projects, ou abra a pasta no Spock primeiro.");

        var matches = workspace.Projects()
            .Where(item => item.ProjectId == project || string.Equals(item.DisplayName, project, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new McpToolException($"'{project}' não é um projeto aberto no Spock. Veja os disponíveis em list_projects."),
            _ => throw new McpToolException($"Há mais de um projeto chamado '{project}'. Use o id de list_projects.")
        };
    }

    private static bool Contains(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(full, root, comparison) || full.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }
}
