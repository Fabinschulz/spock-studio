using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;
using System.Text;

namespace Mariliz.Application.Harness;

/// <summary>
/// Compara o estado atual com o desejado e produz o plano. A comparação é de três vias: a base (o que a
/// aplicação escreveu da última vez, conforme o lock), o disco (o que está lá agora) e o desejado.
/// Um arquivo que a pessoa editou, ou que a aplicação não gerencia, nunca é sobrescrito sem decisão dela.
/// </summary>
public static class PlanBuilder
{
    public static ConfigurationPlan Build(
        string projectId, DiscoveryReport report, IProjectFiles files, ComposeResult composed, ManagedLockFile? existingLock)
    {
        var changes = new List<FileChange>();
        var desiredPaths = composed.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var removalCandidates = composed.Decisions.Where(d => d.Decision == ComponentDecisions.Skip).Select(d => d.ComponentId).ToHashSet(StringComparer.Ordinal);

        foreach (var file in composed.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
            changes.Add(Classify(file, files, existingLock));

        // Remoções: só de arquivos que a aplicação escreveu e que o componente dispensado possuía.
        foreach (var entry in existingLock?.ManagedFiles.Where(e => !desiredPaths.Contains(e.Path) && removalCandidates.Contains(e.Component)) ?? [])
        {
            var current = files.ReadBytes(entry.Path);
            var kind = Catalog.Find(entry.Component)?.Kind ?? ComponentKind.Rule;
            if (current is null) continue;
            var currentHash = Hashing.Sha256(current);

            if (entry.Mode == "kept") continue;
            if (currentHash == entry.Sha256)
                changes.Add(New(entry.Path, kind, entry.Component, ChangeAction.Remove, "O componente foi dispensado; o arquivo gerenciado e intacto sai.",
                    DecisionOrigins.Baseline, currentHash, null, destructive: true, null, Diff(entry.Path, Encoding.UTF8.GetString(current), ""), false));
            else
                changes.Add(New(entry.Path, kind, entry.Component, ChangeAction.Conflict, "O componente foi dispensado, mas o arquivo foi editado por você.",
                    DecisionOrigins.Baseline, currentHash, null, destructive: true, "Arquivo gerenciado editado localmente; só será removido se você decidir.", null, false));
        }

        // Existente e não gerenciado: preservado, e dito no plano.
        var inventoryPreserved = new List<FileChange>();
        foreach (var (item, kind) in InventoryWithKind(report.Inventory))
        {
            if (item.Managed || desiredPaths.Contains(item.Path) || inventoryPreserved.Any(c => c.Path == item.Path)) continue;
            var hash = files.ReadBytes(item.Path) is { } bytes ? Hashing.Sha256(bytes) : null;
            inventoryPreserved.Add(New(item.Path, kind, "existing:" + item.Name, ChangeAction.Preserve,
                item.Issue is null ? "Já existe no projeto e não é gerenciado pela aplicação; permanece como está." : $"Já existe no projeto; permanece como está. Aviso: {item.Issue}",
                DecisionOrigins.Existing, hash, hash, false, null, null, false));
        }
        changes.AddRange(inventoryPreserved.OrderBy(c => c.Path, StringComparer.Ordinal));

        // O lock é estado da aplicação: calculado de tudo o que acima vai existir no disco.
        var lockChange = ClassifyLock(files, existingLock, composed, report, changes);
        if (lockChange is not null) changes.Add(lockChange);

        var planId = PlanIdentity(projectId, composed, changes);
        return new ConfigurationPlan(
            planId, projectId, composed.AiEnvironment, ImplementationModels.Mariliz, composed.ProposalOrigin,
            composed.Decisions, changes, composed.Commands, composed.Limitations, composed.Observations, composed.Risks, PlannedValidations(report));
    }

    private static FileChange Classify(DesiredFile file, IProjectFiles files, ManagedLockFile? lockFile)
    {
        var desired = Normalize(file.Content);
        var afterHash = Hashing.Sha256(desired);
        var currentBytes = files.ReadBytes(file.Path);
        var entry = lockFile?.ManagedFiles.FirstOrDefault(e => e.Path == file.Path);

        if (currentBytes is null)
        {
            var reason = entry is null ? file.Reason : "Arquivo gerenciado ausente no disco; será restaurado. " + file.Reason;
            return New(file.Path, file.Kind, file.ComponentId, ChangeAction.Create, reason, file.Origin, null, afterHash, false, null, Diff(file.Path, null, desired), false);
        }

        var currentText = Encoding.UTF8.GetString(currentBytes);
        var currentHash = Hashing.Sha256(currentBytes);

        if (file.Mode == "merge")
        {
            return Normalize(currentText) == desired
                ? New(file.Path, file.Kind, file.ComponentId, ChangeAction.Preserve, "As chaves gerenciadas já estão como o desejado.", file.Origin, currentHash, currentHash, false, null, null, false)
                : New(file.Path, file.Kind, file.ComponentId, ChangeAction.Update, file.Reason, file.Origin, currentHash, afterHash, false, null, Diff(file.Path, currentText, desired), false);
        }

        if (currentHash == afterHash)
            return New(file.Path, file.Kind, file.ComponentId, ChangeAction.Preserve, "Já está como o desejado.", file.Origin, currentHash, afterHash, false, null, null, false);

        if (entry is { Mode: "file" } && entry.Sha256 == currentHash)
            return New(file.Path, file.Kind, file.ComponentId, ChangeAction.Update, "Conteúdo gerenciado desatualizado; atualizar. " + file.Reason, file.Origin, currentHash, afterHash, false, null, Diff(file.Path, currentText, desired), false);

        if (entry is { Mode: "kept" } && entry.Sha256 == currentHash)
            return New(file.Path, file.Kind, file.ComponentId, ChangeAction.Preserve, "Mantido por decisão sua; a aplicação não o altera.", DecisionOrigins.Existing, currentHash, currentHash, false, null, null, false);

        var why = entry is null
            ? "Já existe um arquivo neste caminho que a aplicação não gerencia."
            : "O arquivo gerenciado foi editado depois da última aplicação.";
        return New(file.Path, file.Kind, file.ComponentId, ChangeAction.Conflict, file.Reason, file.Origin, currentHash, afterHash, destructive: true, why, Diff(file.Path, currentText, desired), false);
    }

    private static FileChange? ClassifyLock(IProjectFiles files, ManagedLockFile? existing, ComposeResult composed, DiscoveryReport report, List<FileChange> changes)
    {
        if (composed.AiEnvironment.Length == 0) return null;
        var current = files.ReadText(ManagedLockFile.FileName);
        // Sem nada gerenciado (ex.: ambiente sem suporte), não há o que registrar.
        if (current is null && composed.Files.All(f => f.Path == DeclaredConfig.FileName) && composed.Commands.Count == 0) return null;
        var pendingSpecification = composed.Commands.Any(c => c.Id is "specification-install" or "specification-update");
        var content = LockBuilder.Build(composed, changes, existing, report.Specification, pendingSpecification);
        var currentHash = current is null ? null : Hashing.Sha256(Normalize(current));

        if (content is null)
        {
            // O hash da estrutura de especificação só existe depois da instalação; o lock é gravado ao final da aplicação.
            return New(ManagedLockFile.FileName, ComponentKind.Config, Catalog.ConfigDeclaration,
                current is null ? ChangeAction.Create : ChangeAction.Update,
                "Registro dos arquivos gerenciados e da estrutura de especificação. O conteúdo é calculado depois da instalação dela.",
                DecisionOrigins.Baseline, currentHash, null, false, null, null, false);
        }

        var afterHash = Hashing.Sha256(Normalize(content));
        if (currentHash == afterHash)
            return New(ManagedLockFile.FileName, ComponentKind.Config, Catalog.ConfigDeclaration, ChangeAction.Preserve,
                "O registro já está como o desejado.", DecisionOrigins.Baseline, currentHash, afterHash, false, null, null, false);

        return New(ManagedLockFile.FileName, ComponentKind.Config, Catalog.ConfigDeclaration,
            current is null ? ChangeAction.Create : ChangeAction.Update,
            "Registro dos arquivos gerenciados, das versões e da estrutura de especificação.", DecisionOrigins.Baseline, currentHash, afterHash, false, null,
            Diff(ManagedLockFile.FileName, current, Normalize(content)), false);
    }

    private static IEnumerable<(InventoryItem Item, ComponentKind Kind)> InventoryWithKind(HarnessInventory inv)
    {
        foreach (var i in inv.Agents) yield return (i, ComponentKind.Agent);
        foreach (var i in inv.Skills.Where(s => !s.Path.StartsWith(ManagedScope.SpecificationDirectory, StringComparison.Ordinal))) yield return (i, ComponentKind.Skill);
        foreach (var i in inv.Rules) yield return (i, ComponentKind.Rule);
        foreach (var i in inv.Hooks.Where(h => h.Path != Catalog.PathSettings)) yield return (i, ComponentKind.Hook);
        foreach (var i in inv.Evals) yield return (i, ComponentKind.Eval);
    }

    /// <summary>O que a validação executa depois de aplicar, dito antes para a pessoa aprovar sabendo.</summary>
    private static IReadOnlyList<string> PlannedValidations(DiscoveryReport report)
    {
        var python = report.Tools.PythonCommand ?? "python";
        return
        [
            "Conferir arquivos, hashes, agentes, skills, regras, referências e ausência de segredos (somente leitura).",
            $"Executar o script do hook com entradas conhecidas: {python} .claude/hooks/mariliz-policy.py (sem rede, sem escrita).",
            $"Executar as validações da especificação: {python} <skill>/scripts/check_commit.py e lessons.py selftest (somente leitura).",
            "Gerar um novo plano e confirmar que ele não propõe nenhuma mudança (idempotência)."
        ];
    }

    private static string PlanIdentity(string projectId, ComposeResult composed, IReadOnlyList<FileChange> changes)
    {
        var sb = new StringBuilder();
        sb.Append(projectId).Append('|').Append(composed.AiEnvironment).Append('|').Append(composed.ProposalOrigin).Append('\n');
        foreach (var c in changes.OrderBy(c => c.Path, StringComparer.Ordinal))
            sb.Append(c.Path).Append('|').Append(c.Action).Append('|').Append(c.BeforeHash).Append('|').Append(c.AfterHash).Append('\n');
        foreach (var cmd in composed.Commands)
            sb.Append(cmd.Id).Append('|').Append(cmd.Executable).Append(' ').Append(string.Join(' ', cmd.Arguments)).Append('\n');
        return "plan-" + Hashing.Sha256(sb.ToString())[..16];
    }

    private static FileChange New(string path, ComponentKind kind, string componentId, ChangeAction action, string reason, string origin,
        string? before, string? after, bool destructive, string? conflict, (string Diff, bool Truncated)? diff, bool _) =>
        new("chg-" + Hashing.Sha256(path)[..10], kind, componentId, path, action, reason, origin, before, after, destructive, conflict, diff?.Diff, diff?.Truncated ?? false);

    private static FileChange New(string path, ComponentKind kind, string componentId, ChangeAction action, string reason, string origin,
        string? before, string? after, bool destructive, string? conflict, (string Diff, bool Truncated)? diff) =>
        New(path, kind, componentId, action, reason, origin, before, after, destructive, conflict, diff, false);

    /// <summary>O diff é mostrado na tela e vai no plano: linhas com formato de credencial são omitidas dos dois lados.</summary>
    private static (string Diff, bool Truncated)? Diff(string path, string? before, string after) =>
        UnifiedDiff.Create(path,
            before is null ? null : Core.Policy.SecretScanner.RedactLines(Normalize(before)),
            Core.Policy.SecretScanner.RedactLines(after));

    /// <summary>Fim de linha e espaço final uniformes: o hash e o diff não dependem do sistema que escreveu.</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n");
}
