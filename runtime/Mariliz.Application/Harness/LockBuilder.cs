using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>Calcula o conteúdo do <c>mariliz.lock</c> a partir do que existirá no disco depois da aplicação.</summary>
public static class LockBuilder
{
    /// <returns><c>null</c> quando o conteúdo depende da instalação da estrutura de especificação, ainda pendente.</returns>
    public static string? Build(ComposeResult composed, IReadOnlyList<FileChange> changes, ManagedLockFile? existing, SpecificationStatus specification, bool specificationPending)
    {
        if (specificationPending) return null;

        var lockFile = new ManagedLockFile
        {
            Engine = BuildInfo.Version,
            AiEnvironment = composed.AiEnvironment,
            Specification = new ManagedLockFile.SpecificationSection { Version = specification.Version ?? "", ContentHash = specification.ComputedHash ?? "" }
        };

        var byPath = composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (!byPath.TryGetValue(change.Path, out var desired) || change.Path == DeclaredConfig.FileName) continue;
            if (EntryFor(change, desired, existing) is { } entry) lockFile.ManagedFiles.Add(entry);
        }

        var componentIds = lockFile.ManagedFiles.Select(f => f.Component).Distinct().ToList();
        foreach (var id in componentIds)
        {
            var component = Catalog.Find(id);
            lockFile.Components.Add(new() { Id = id, Kind = (component?.Kind ?? ComponentKind.Rule).ToString().ToLowerInvariant(), Version = ComponentVersion(id, component, specification) });
        }
        return lockFile.ToYaml();
    }

    internal static string BuildFromOutcomes(ComposeResult composed, IReadOnlyDictionary<string, Execution.FileOutcome> outcomes, ManagedLockFile? existing, SpecificationStatus specification)
    {
        var lockFile = new ManagedLockFile
        {
            Engine = BuildInfo.Version,
            AiEnvironment = composed.AiEnvironment,
            Specification = new ManagedLockFile.SpecificationSection
            {
                Version = specification.State == SpecificationState.Installed ? specification.Version ?? "" : "",
                ContentHash = specification.State == SpecificationState.Installed ? specification.ComputedHash ?? "" : ""
            }
        };

        var desired = composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var (path, outcome) in outcomes)
        {
            // mariliz.yaml é da pessoa depois de criado: a aplicação não o registra como gerenciado.
            if (!desired.TryGetValue(path, out var file) || path == DeclaredConfig.FileName) continue;
            if (EntryFromOutcome(path, file, outcome, existing) is { } entry) lockFile.ManagedFiles.Add(entry);
        }

        // O que a aplicação já gerenciava e esta execução não tocou continua registrado.
        foreach (var prior in existing?.ManagedFiles ?? [])
            if (!desired.ContainsKey(prior.Path) && !outcomes.ContainsKey(prior.Path)) lockFile.ManagedFiles.Add(prior);

        foreach (var id in lockFile.ManagedFiles.Select(f => f.Component).Distinct())
        {
            var component = Catalog.Find(id);
            lockFile.Components.Add(new() { Id = id, Kind = (component?.Kind ?? ComponentKind.Rule).ToString().ToLowerInvariant(), Version = ComponentVersion(id, component, specification) });
        }
        return lockFile.ToYaml();
    }

    private static ManagedLockFile.ManagedFileEntry? EntryFor(FileChange change, DesiredFile desired, ManagedLockFile? existing) =>
        change.Action switch
        {
            ChangeAction.Create or ChangeAction.Update => Entry(change.Path, desired.ComponentId, desired.Mode, change.AfterHash ?? ""),
            ChangeAction.Preserve when change.Origin == DecisionOrigins.Existing => Entry(change.Path, desired.ComponentId, "kept", change.BeforeHash ?? ""),
            ChangeAction.Preserve => Entry(change.Path, desired.ComponentId, desired.Mode, change.AfterHash ?? ""),
            // Resolvido na aplicação: se mantido, registra-se como "kept" naquele momento.
            ChangeAction.Conflict => existing?.ManagedFiles.FirstOrDefault(e => e.Path == change.Path),
            _ => null
        };

    private static ManagedLockFile.ManagedFileEntry? EntryFromOutcome(string path, DesiredFile desired, Execution.FileOutcome outcome, ManagedLockFile? existing) =>
        outcome.Kind switch
        {
            Execution.OutcomeKind.Written or Execution.OutcomeKind.Preserved => Entry(path, desired.ComponentId, desired.Mode, outcome.Hash ?? ""),
            Execution.OutcomeKind.Kept => Entry(path, desired.ComponentId, "kept", outcome.Hash ?? ""),
            Execution.OutcomeKind.NotApplied => existing?.ManagedFiles.FirstOrDefault(e => e.Path == path),
            _ => null
        };

    private static ManagedLockFile.ManagedFileEntry Entry(string path, string component, string mode, string hash) =>
        new() { Path = path, Component = component, Mode = mode, Sha256 = hash };

    /// <summary>A estrutura de especificação é externa: a versão registrada é a da instalação verificada, nunca um valor fixo do catálogo.</summary>
    private static string ComponentVersion(string id, CatalogComponent? component, SpecificationStatus specification) =>
        id == Catalog.SpecificationStructure ? (specification.State == SpecificationState.Installed ? specification.Version ?? "" : "") : component?.Version ?? Catalog.ComponentVersion;
}
