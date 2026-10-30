using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>Calcula o conteúdo do <c>mariliz.lock</c> a partir do que existirá no disco depois da aplicação.</summary>
public static class LockBuilder
{
    /// <returns><c>null</c> quando o conteúdo depende de uma instalação do TLC ainda pendente.</returns>
    public static string? Build(ComposeResult composed, IReadOnlyList<FileChange> changes, ManagedLockFile? existing, TlcStatus tlc, bool tlcPending)
    {
        if (tlcPending) return null;

        var lockFile = new ManagedLockFile
        {
            Engine = BuildInfo.Version,
            AiEnvironment = composed.AiEnvironment,
            Sdd = new ManagedLockFile.SddSection { Method = "tlc", Version = tlc.Version ?? "", ContentHash = tlc.ComputedHash ?? "" }
        };

        var byPath = composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var change in changes)
        {
            if (!byPath.TryGetValue(change.Path, out var desired) || change.Path == DeclaredConfig.FileName) continue;
            switch (change.Action)
            {
                case ChangeAction.Create or ChangeAction.Update:
                    lockFile.ManagedFiles.Add(new() { Path = change.Path, Component = desired.ComponentId, Mode = desired.Mode, Sha256 = change.AfterHash ?? "" });
                    break;
                case ChangeAction.Preserve when change.Origin == DecisionOrigins.Existing:
                    lockFile.ManagedFiles.Add(new() { Path = change.Path, Component = desired.ComponentId, Mode = "kept", Sha256 = change.BeforeHash ?? "" });
                    break;
                case ChangeAction.Preserve:
                    lockFile.ManagedFiles.Add(new() { Path = change.Path, Component = desired.ComponentId, Mode = desired.Mode, Sha256 = change.AfterHash ?? "" });
                    break;
                case ChangeAction.Conflict:
                    // Resolvido na aplicação: se mantido, registra-se como "kept" naquele momento.
                    var prior = existing?.ManagedFiles.FirstOrDefault(e => e.Path == change.Path);
                    if (prior is not null) lockFile.ManagedFiles.Add(prior);
                    break;
            }
        }

        var componentIds = lockFile.ManagedFiles.Select(f => f.Component).Distinct().ToList();
        foreach (var id in componentIds)
        {
            var component = Catalog.Find(id);
            lockFile.Components.Add(new() { Id = id, Kind = (component?.Kind ?? ComponentKind.Rule).ToString().ToLowerInvariant(), Version = ComponentVersion(id, component, tlc) });
        }
        return lockFile.ToYaml();
    }

    internal static string BuildFromOutcomes(ComposeResult composed, IReadOnlyDictionary<string, Execution.FileOutcome> outcomes, ManagedLockFile? existing, TlcStatus tlc)
    {
        var lockFile = new ManagedLockFile
        {
            Engine = BuildInfo.Version,
            AiEnvironment = composed.AiEnvironment,
            Sdd = new ManagedLockFile.SddSection
            {
                Method = "tlc",
                Version = tlc.State == TlcState.Installed ? tlc.Version ?? "" : "",
                ContentHash = tlc.State == TlcState.Installed ? tlc.ComputedHash ?? "" : ""
            }
        };

        var desired = composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var (path, outcome) in outcomes)
        {
            // mariliz.yaml é da pessoa depois de criado: a aplicação não o registra como gerenciado.
            if (!desired.TryGetValue(path, out var d) || path == DeclaredConfig.FileName) continue;
            switch (outcome.Kind)
            {
                case Execution.OutcomeKind.Written or Execution.OutcomeKind.Preserved:
                    lockFile.ManagedFiles.Add(new() { Path = path, Component = d.ComponentId, Mode = d.Mode, Sha256 = outcome.Hash ?? "" });
                    break;
                case Execution.OutcomeKind.Kept:
                    lockFile.ManagedFiles.Add(new() { Path = path, Component = d.ComponentId, Mode = "kept", Sha256 = outcome.Hash ?? "" });
                    break;
                case Execution.OutcomeKind.NotApplied:
                    if (existing?.ManagedFiles.FirstOrDefault(e => e.Path == path) is { } prior) lockFile.ManagedFiles.Add(prior);
                    break;
            }
        }

        // O que a aplicação já gerenciava e esta execução não tocou continua registrado.
        foreach (var prior in existing?.ManagedFiles ?? [])
            if (!desired.ContainsKey(prior.Path) && !outcomes.ContainsKey(prior.Path)) lockFile.ManagedFiles.Add(prior);

        foreach (var id in lockFile.ManagedFiles.Select(f => f.Component).Distinct())
        {
            var component = Catalog.Find(id);
            lockFile.Components.Add(new() { Id = id, Kind = (component?.Kind ?? ComponentKind.Rule).ToString().ToLowerInvariant(), Version = ComponentVersion(id, component, tlc) });
        }
        return lockFile.ToYaml();
    }

    /// <summary>O TLC é externo: a versão registrada é a da instalação verificada, nunca um valor fixo do catálogo.</summary>
    private static string ComponentVersion(string id, CatalogComponent? component, TlcStatus tlc) =>
        id == Catalog.SddTlc ? (tlc.State == TlcState.Installed ? tlc.Version ?? "" : "") : component?.Version ?? Catalog.ComponentVersion;
}
