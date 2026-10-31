using System.Security.Cryptography;
using System.Text;

namespace Mariliz.Application.Workspace;

/// <summary>
/// Registra as pastas que o usuário escolheu no diálogo nativo. É a única origem de caminhos
/// que o motor aceita; qualquer outro caminho fica fora da lista autorizada.
/// </summary>
public sealed class WorkspaceAuthorizer(IWorkspaceFileSystem fileSystem)
{
    private readonly Dictionary<string, string> _authorized = new();

    public WorkspaceResult Authorize(string path)
    {
        var resolved = string.IsNullOrWhiteSpace(path) ? null : fileSystem.ResolveDirectory(path);
        if (resolved is null) return WorkspaceResult.Fail(WorkspaceError.NotFound);

        var trimmed = resolved.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Raiz de unidade ou de sistema não é um projeto.
        if (trimmed.Length == 0 || Directory.GetParent(trimmed) is null || Path.GetPathRoot(resolved) == resolved)
            return WorkspaceResult.Fail(WorkspaceError.NotAllowed);

        if (!fileSystem.CanEnumerate(resolved)) return WorkspaceResult.Fail(WorkspaceError.NotReadable);

        var projectId = "p-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(trimmed))))[..12].ToLowerInvariant();
        _authorized[projectId] = trimmed;

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

    private static string Normalize(string path) =>
        OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;

    /// <summary>Troca a pasta do usuário por <c>~</c> e mantém só o início e os dois últimos níveis.</summary>
    internal string Abbreviate(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        var home = fileSystem.HomeDirectory.TrimEnd(sep);
        var shown = home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;

        var parts = shown.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 4) return shown;
        return string.Join(sep, parts[0], "…", parts[^2], parts[^1]);
    }
}
