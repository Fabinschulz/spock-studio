using Mariliz.Application.Execution;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>
/// Conteúdo do <c>mariliz.lock</c>. O plano o prevê a partir das mudanças; a aplicação o grava a partir do que de fato
/// aconteceu. As duas origens montam as entradas de forma diferente, mas o arquivo é escrito por uma só regra: se a
/// previsão e a gravação divergissem, o plano seguinte mostraria uma atualização do lock que nunca termina.
/// </summary>
public static class LockBuilder
{
    /// <summary>Lock previsto pelo plano, supondo que tudo o que o plano propõe seja aplicado.</summary>
    /// <returns><c>null</c> quando o conteúdo depende da instalação da estrutura de especificação, ainda pendente.</returns>
    public static string? Build(ComposeResult composed, IReadOnlyList<FileChange> changes, ManagedLockFile? existing, SpecificationStatus specification, bool specificationPending)
    {
        if (specificationPending) return null;

        var desired = Desired(composed);
        var entries = changes
            .Where(change => change.Path != DeclaredConfig.FileName && desired.ContainsKey(change.Path))
            .Select(change => EntryFor(change, desired[change.Path], existing))
            .OfType<ManagedLockFile.ManagedFileEntry>();
        return Write(composed, entries, specification);
    }

    /// <summary>Lock gravado depois da aplicação, a partir do resultado de cada arquivo.</summary>
    internal static string BuildFromOutcomes(ComposeResult composed, IReadOnlyDictionary<string, FileOutcome> outcomes, ManagedLockFile? existing, SpecificationStatus specification)
    {
        var desired = Desired(composed);
        var written = outcomes
            .Where(pair => pair.Key != DeclaredConfig.FileName && desired.ContainsKey(pair.Key))
            .Select(pair => EntryFromOutcome(pair.Key, desired[pair.Key], pair.Value, existing))
            .OfType<ManagedLockFile.ManagedFileEntry>();

        // O que a aplicação já gerenciava e esta execução não tocou (remoção não aprovada) continua registrado.
        var untouched = (existing?.ManagedFiles ?? []).Where(prior => !desired.ContainsKey(prior.Path) && !outcomes.ContainsKey(prior.Path));
        return Write(composed, written.Concat(untouched), specification);
    }

    /// <summary>mariliz.yaml é da pessoa depois de criado: nunca entra como gerenciado (filtrado pelos chamadores).</summary>
    private static Dictionary<string, DesiredFile> Desired(ComposeResult composed) =>
        composed.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);

    private static string Write(ComposeResult composed, IEnumerable<ManagedLockFile.ManagedFileEntry> entries, SpecificationStatus specification)
    {
        // Só uma instalação verificada da estrutura de especificação é registrada.
        var installed = specification.State == SpecificationState.Installed;
        var lockFile = new ManagedLockFile
        {
            Engine = BuildInfo.Version,
            AiEnvironment = composed.AiEnvironment,
            Specification = new ManagedLockFile.SpecificationSection
            {
                Version = installed ? specification.Version ?? "" : "",
                ContentHash = installed ? specification.ComputedHash ?? "" : ""
            }
        };
        lockFile.ManagedFiles.AddRange(entries);

        foreach (var id in lockFile.ManagedFiles.Select(f => f.Component).Distinct())
        {
            var component = Catalog.Find(id);
            lockFile.Components.Add(new() { Id = id, Kind = (component?.Kind ?? ComponentKind.Rule).ToString().ToLowerInvariant(), Version = ComponentVersion(id, component, installed ? specification.Version : null) });
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

    private static ManagedLockFile.ManagedFileEntry? EntryFromOutcome(string path, DesiredFile desired, FileOutcome outcome, ManagedLockFile? existing) =>
        outcome.Kind switch
        {
            OutcomeKind.Written or OutcomeKind.Preserved => Entry(path, desired.ComponentId, desired.Mode, outcome.Hash ?? ""),
            OutcomeKind.Kept => Entry(path, desired.ComponentId, "kept", outcome.Hash ?? ""),
            OutcomeKind.NotApplied => existing?.ManagedFiles.FirstOrDefault(e => e.Path == path),
            _ => null
        };

    private static ManagedLockFile.ManagedFileEntry Entry(string path, string component, string mode, string hash) =>
        new() { Path = path, Component = component, Mode = mode, Sha256 = hash };

    /// <summary>A estrutura de especificação é externa: a versão registrada é a da instalação verificada, nunca um valor fixo do catálogo.</summary>
    private static string ComponentVersion(string id, CatalogComponent? component, string? installedSpecification) =>
        id == Catalog.SpecificationStructure ? installedSpecification ?? "" : component?.Version ?? Catalog.ComponentVersion;
}
