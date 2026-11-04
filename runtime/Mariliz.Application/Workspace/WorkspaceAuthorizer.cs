using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workspace;

/// <summary>
/// Registra as pastas que o usuário escolheu no diálogo nativo. É a única origem de caminhos
/// que o motor aceita; qualquer outro caminho fica fora da lista autorizada.
/// Se houver um registro, a autorização sobrevive ao reinício do processo e continua fora da interface.
/// </summary>
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

    /// <summary>Raiz absoluta de um projeto já autorizado. É a única forma de resolver um id em caminho.</summary>
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
        "p-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(trimmed))))[..12].ToLowerInvariant();

    private static string Normalize(string path) =>
        OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;

    /// <summary>Troca a pasta do usuário por <c>~</c> e mantém só o início e os dois últimos níveis.</summary>
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
