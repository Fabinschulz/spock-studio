using System.Collections.Concurrent;
using Mariliz.Application.Harness;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workspace;

// O diálogo nativo é a única origem de caminhos que o motor aceita.
public sealed class WorkspaceAuthorizer
{
    private readonly IWorkspaceFileSystem _fileSystem;
    private readonly IWorkspaceRegistry? _registry;
    // Escrito pelo protocolo (autorização) e lido pelas execuções em segundo plano.
    private readonly ConcurrentDictionary<string, string> _authorized = new();
    private readonly ConcurrentDictionary<string, string> _names = new();
    private readonly ConcurrentDictionary<string, string> _proposedStableIds = new();
    private readonly object _proposalGate = new();

    public WorkspaceAuthorizer(IWorkspaceFileSystem fileSystem, IWorkspaceRegistry? registry = null)
    {
        _fileSystem = fileSystem;
        _registry = registry;
        if (registry is null) return;

        foreach (var (id, stableId) in registry.LoadProposedStableIds())
            if (Ids.IsProjectId(id) && StableProjectId.IsValid(stableId)) _proposedStableIds[id] = stableId;
        foreach (var (id, path) in registry.Load())
        {
            if (!Ids.IsProjectId(id)) continue;
            var resolved = fileSystem.ResolveDirectory(path);
            if (resolved is null) continue;
            var trimmed = Trim(resolved);
            if (!IsProjectRoot(resolved, trimmed) || !fileSystem.CanEnumerate(resolved)) continue;
            if (ProjectIdFor(trimmed) != id) continue;
            _authorized[id] = trimmed;
            _names[id] = NameOf(trimmed);
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
        var name = NameOf(trimmed);
        _names[projectId] = name;
        _authorized[projectId] = trimmed;
        _registry?.Save(projectId, trimmed);

        return WorkspaceResult.Ok(new AuthorizedProject(projectId, name, Abbreviate(trimmed)));
    }

    public bool IsAuthorized(string projectId) => _authorized.ContainsKey(projectId);

    // O caminho completo continua só no motor.
    public IReadOnlyList<AuthorizedProject> Projects() =>
        _authorized
            .Select(pair => new AuthorizedProject(pair.Key, _names.GetValueOrDefault(pair.Key) ?? Path.GetFileName(pair.Value), Abbreviate(pair.Value)))
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

    // F1: o id estável que está na declaração da cópia (spock.yaml, ou mariliz.yaml num projeto anterior à troca de nome). Lido a cada pergunta: um plano aplicado o grava sem passar por aqui.
    public HarnessNaming NamingOf(string projectId) =>
        TryGetRoot(projectId, out var root)
            ? HarnessNaming.Detect(name => _fileSystem.RootFiles(root).Contains(name, StringComparer.Ordinal),
                directory => _fileSystem.ResolveDirectory(Path.Combine(root, directory)) is not null)
            : HarnessNaming.Spock;

    public string? StableIdOf(string projectId) =>
        TryGetRoot(projectId, out var root) ? HarnessNaming.All.Select(naming => DeclaredConfig.StableIdIn(_fileSystem.ReadRootFile(root, naming.Declaration))).FirstOrDefault(id => id is not null) : null;

    // O id que o plano grava quando a declaração ainda não tem um: o mesmo até ser aplicado, mesmo replanejando ou reiniciando.
    public string ProposedStableId(string projectId)
    {
        if (StableIdOf(projectId) is { } declared) return declared;
        lock (_proposalGate)
        {
            if (_proposedStableIds.TryGetValue(projectId, out var proposed)) return proposed;
            var created = StableProjectId.New(DateTimeOffset.UtcNow);
            _registry?.SaveProposedStableId(projectId, created);
            _proposedStableIds[projectId] = created;
            return created;
        }
    }

    private string NameOf(string root) =>
        ProjectNameReader.Read(_fileSystem.RootFiles(root), file => _fileSystem.ReadRootFile(root, file)) ?? Path.GetFileName(root);

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
