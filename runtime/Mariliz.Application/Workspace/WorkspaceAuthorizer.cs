using System.Collections.Concurrent;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workspace;

// O diálogo nativo é a única origem de caminhos que o motor aceita.
public sealed class WorkspaceAuthorizer
{
    private readonly IWorkspaceFileSystem _fileSystem;
    private readonly IWorkspaceRegistry? _registry;
    // Escrito pelo protocolo (autorização) e lido pelas execuções em segundo plano.
    private readonly ConcurrentDictionary<string, string> _authorized = new();

    public WorkspaceAuthorizer(IWorkspaceFileSystem fileSystem, IWorkspaceRegistry? registry = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        if (registry is null) return;

        foreach (var (id, path) in registry.Load())
        {
            if (!Ids.IsProjectId(id)) continue;
            var resolved = fileSystem.ResolveDirectory(path);
            if (resolved is null) continue;
            var trimmed = Trim(resolved);
            if (!IsProjectRoot(resolved, trimmed) || !fileSystem.CanEnumerate(resolved)) continue;
            if (ProjectIdFor(trimmed) != id) continue;
            _authorized[id] = trimmed;
        }
    }

    public WorkspaceResult Authorize(string path)
    {
        var resolved = string.IsNullOrWhiteSpace(path) ? null : _fileSystem.ResolveDirectory(path);
        if (resolved is null) return WorkspaceResult.Fail(WorkspaceError.NotFound);

        var trimmed = Trim(resolved);
        if (!IsProjectRoot(resolved, trimmed)) return WorkspaceResult.Fail(WorkspaceError.NotAllowed);
        if (!_fileSystem.CanEnumerate(resolved)) return WorkspaceResult.Fail(WorkspaceError.NotReadable);

        var projectId = ProjectIdFor(trimmed);
        _authorized[projectId] = trimmed;
        _registry?.Save(projectId, trimmed);

        return WorkspaceResult.Ok(new AuthorizedProject(projectId, Path.GetFileName(trimmed), Abbreviate(trimmed)));
    }

    public bool IsAuthorized(string projectId) => _authorized.ContainsKey(projectId);

    // O caminho completo continua só no motor.
    public IReadOnlyList<AuthorizedProject> Projects() =>
        _authorized
            .Select(pair => new AuthorizedProject(pair.Key, Path.GetFileName(pair.Value), Abbreviate(pair.Value)))
            .OrderBy(project => project.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(project => project.ProjectId, StringComparer.Ordinal)
            .ToList();

    // É a única forma de resolver um id em caminho.
    public bool TryGetRoot(string projectId, out string root)
    {
        var found = _authorized.TryGetValue(projectId, out var value);
        root = value ?? "";
        return found;
    }

    private static string Trim(string path) => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsProjectRoot(string resolved, string trimmed) =>
        trimmed.Length > 0 && Directory.GetParent(trimmed) is not null && Path.GetPathRoot(resolved) != resolved;

    private static string ProjectIdFor(string trimmed) =>
        "p-" + Hashing.Sha256(Normalize(trimmed))[..12];

    private static string Normalize(string path) =>
        OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;

    internal string Abbreviate(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        var home = _fileSystem.HomeDirectory.TrimEnd(sep);
        var shown = home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;

        var parts = shown.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 4) return shown;
        return string.Join(sep, parts[0], "…", parts[^2], parts[^1]);
    }
}
