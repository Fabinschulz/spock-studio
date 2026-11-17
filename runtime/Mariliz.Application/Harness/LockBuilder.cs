using Mariliz.Application.Execution;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

// O plano o prevê e a aplicação o grava a partir do que aconteceu, mas o arquivo é escrito por uma só regra: se a previsão e a gravação divergissem, o plano seguinte mostraria uma atualização do lock que nunca termina.
public static class LockBuilder
{
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

    // mariliz.yaml é da pessoa depois de criado: nunca entra como gerenciado. A saída do hook do settings.json ("release") também não: depois dela o arquivo não tem mais nada gerenciado.
    private static Dictionary<string, DesiredFile> Desired(ComposeResult composed) =>
        composed.Files.Where(f => f.Mode != "release").ToDictionary(f => f.Path, StringComparer.Ordinal);

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
            ChangeAction.Create or ChangeAction.Update => Entry(change.Path, desired, desired.Mode, change.AfterHash ?? ""),
            ChangeAction.Preserve when change.Origin == DecisionOrigins.Existing => Entry(change.Path, desired.ComponentId, "kept", change.BeforeHash ?? ""),
            ChangeAction.Preserve => Entry(change.Path, desired, desired.Mode, change.AfterHash ?? ""),
            // Resolvido na aplicação: se mantido, registra-se como "kept" naquele momento.
            ChangeAction.Conflict => existing?.ManagedFiles.FirstOrDefault(e => e.Path == change.Path),
            _ => null
        };

    private static ManagedLockFile.ManagedFileEntry? EntryFromOutcome(string path, DesiredFile desired, FileOutcome outcome, ManagedLockFile? existing) =>
        outcome.Kind switch
        {
            OutcomeKind.Written or OutcomeKind.Preserved => Entry(path, desired, desired.Mode, outcome.Hash ?? ""),
            OutcomeKind.Kept => Entry(path, desired.ComponentId, "kept", outcome.Hash ?? ""),
            OutcomeKind.NotApplied => existing?.ManagedFiles.FirstOrDefault(e => e.Path == path),
            _ => null
        };

    private static ManagedLockFile.ManagedFileEntry Entry(string path, string component, string mode, string hash) =>
        new() { Path = path, Component = component, Mode = mode, Sha256 = hash };

    // Num trecho, o arquivo inteiro é da pessoa: o registro guarda o hash do trecho, que é o que o próximo plano compara.
    private static ManagedLockFile.ManagedFileEntry Entry(string path, DesiredFile desired, string mode, string fileHash) =>
        ManagedRegion.IsRegionMode(mode) && ManagedRegion.Extract(mode, desired.Region!, desired.Content) is { } region
            ? new() { Path = path, Component = desired.ComponentId, Mode = mode, Region = desired.Region, Sha256 = ManagedRegion.Hash(region) }
            : Entry(path, desired.ComponentId, mode, fileHash);

    // A estrutura de especificação é externa: a versão é a da instalação verificada, nunca um valor fixo do catálogo.
    private static string ComponentVersion(string id, CatalogComponent? component, string? installedSpecification) =>
        id == Catalog.SpecificationStructure ? installedSpecification ?? "" : component?.Version ?? Catalog.ComponentVersion;
}
