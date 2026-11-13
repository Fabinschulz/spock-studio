using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Engineering;

// Mesma regra da verificação do lock na validação: arquivo inteiro pelo hash; settings.json (modo merge) pela presença do hook gerenciado.
public sealed class HarnessStatusService(WorkspaceAuthorizer workspace, IProjectFilesFactory files, IClock clock)
{
    private static readonly string[] Severity = ["intact", "kept", "modified", "outOfScope", "missing"];

    public HarnessStatus Inspect(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        var project = files.Open(root);
        var excluded = (DeclaredConfig.TryRead(project).Config?.Components?.Exclude ?? [])
            .Select(id => new HarnessExcludedComponent(id, HarnessLabels.Of(id), ComponentRequest.IsManageable(id)))
            .ToList();
        var now = clock.UtcNow;

        var lockExists = project.FileExists(ManagedLockFile.FileName);
        var lockFile = lockExists ? ManagedLockFile.Read(project) : null;
        if (lockFile is null)
            return new HarnessStatus(projectId, now, lockExists, !lockExists, null, null, null, [], excluded, 0, 0, 0, 0);

        var filesByComponent = lockFile.ManagedFiles
            .Select(entry => (entry.Component, Status: FileStatus(project, entry)))
            .GroupBy(item => item.Component, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Status).ToList(), StringComparer.Ordinal);

        var components = lockFile.Components
            .Select(component =>
            {
                var componentFiles = filesByComponent.GetValueOrDefault(component.Id) ?? [];
                var state = componentFiles.Count == 0 ? "intact" : componentFiles.MaxBy(file => Array.IndexOf(Severity, file.State))!.State;
                return new HarnessComponentStatus(component.Id, HarnessLabels.Of(component.Id), component.Kind, component.Version, state,
                    ComponentRequest.IsManageable(component.Id), componentFiles);
            })
            .OrderByDescending(component => Array.IndexOf(Severity, component.State))
            .ThenBy(component => component.Label, StringComparer.Ordinal)
            .ToList();

        return new HarnessStatus(
            projectId,
            now,
            true,
            true,
            NullIfEmpty(lockFile.Engine),
            NullIfEmpty(lockFile.AiEnvironment),
            NullIfEmpty(lockFile.Specification.Version),
            components,
            excluded,
            components.Count(component => component.State == "intact"),
            components.Count(component => component.State == "kept"),
            components.Count(component => component.State is "modified" or "outOfScope"),
            components.Count(component => component.State == "missing"));
    }

    private static HarnessFileStatus FileStatus(IProjectFiles project, ManagedLockFile.ManagedFileEntry entry)
    {
        if (!PathGuard.TryNormalize(entry.Path, out var path, out _) || !ManagedScope.IsWritable(path))
            return new HarnessFileStatus(entry.Path, entry.Mode, "outOfScope");

        var bytes = project.ReadBytes(path);
        if (bytes is null) return new HarnessFileStatus(path, entry.Mode, "missing");

        if (entry.Mode == "merge")
        {
            var hookRegistered = path != Catalog.PathSettings
                || System.Text.Encoding.UTF8.GetString(bytes).Contains(Catalog.PathHookScript, StringComparison.Ordinal);
            return new HarnessFileStatus(path, entry.Mode, hookRegistered ? "intact" : "modified");
        }

        // "kept": a pessoa escolheu manter a própria versão num conflito; o lock guarda o hash dela.
        var unchanged = Hashing.Sha256(bytes) == entry.Sha256;
        return new HarnessFileStatus(path, entry.Mode, !unchanged ? "modified" : entry.Mode == "kept" ? "kept" : "intact");
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
