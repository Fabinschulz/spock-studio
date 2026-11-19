using Mariliz.Application.Engineering;

namespace Mariliz.Application.Compatibility;

/// <param name="Kind">changed, added ou removed, em relação ao perfil analisado no Spock.</param>
public sealed record VersionChange(
    string Root,
    string TechnologyId,
    string Name,
    string Origin,
    string Kind,
    string? From,
    string? To,
    IReadOnlyList<string> Evidence);

/// <param name="BaselineAt">Quando o perfil comparado foi analisado; nulo sem perfil.</param>
/// <param name="Clean">Nenhuma versão mudou e nenhuma combinação está incompatível.</param>
public sealed record VersionCheckReport(
    string ProjectId,
    DateTimeOffset? BaselineAt,
    IReadOnlyList<VersionChange> Changes,
    IReadOnlyList<ModuleCompatibility> Incompatible,
    bool Clean,
    string Guidance);

// MV-3: depois da tarefa, o que o agente mudou nas versões do projeto. A referência é o perfil que o Spock analisou, não o
// git: o perfil é o que foi entregue ao agente como restrição. Só compara; nunca grava o perfil nem mexe no projeto.
public static class VersionCheck
{
    public const string Changed = "changed";
    public const string Added = "added";
    public const string Removed = "removed";

    public static VersionCheckReport Compare(string projectId, TechnologyProfile? baseline, IReadOnlyList<ProjectModule> current,
        ReleaseCatalog catalog, DateTimeOffset now)
    {
        var changes = baseline is null || baseline.Modules.Count == 0 ? [] : Diff(baseline.Modules, current);
        var incompatible = CompatibilityAssessor.Assess(current, catalog, now).Modules
            .Where(module => module.Status == SupportStatuses.Incompatible).ToList();
        var clean = changes.Count == 0 && incompatible.Count == 0;
        return new VersionCheckReport(projectId, baseline?.AnalyzedAt, changes, incompatible, clean,
            Guidance(baseline, changes, incompatible));
    }

    private static List<VersionChange> Diff(IReadOnlyList<ProjectModule> before, IReadOnlyList<ProjectModule> after)
    {
        var old = before.GroupBy(Key).ToDictionary(group => group.Key, group => group.First());
        var now = after.GroupBy(Key).ToDictionary(group => group.Key, group => group.First());
        var changes = new List<VersionChange>();
        foreach (var (key, module) in now)
        {
            if (!old.TryGetValue(key, out var was))
                changes.Add(Change(module, Added, null, module.Version));
            else if (!string.Equals(was.Version, module.Version, StringComparison.Ordinal))
                changes.Add(Change(module, Changed, was.Version, module.Version));
        }
        foreach (var (key, module) in old.Where(entry => !now.ContainsKey(entry.Key)))
            changes.Add(Change(module, Removed, module.Version, null));
        return changes.OrderBy(change => change.Root, StringComparer.Ordinal).ThenBy(change => change.TechnologyId, StringComparer.Ordinal).ToList();
    }

    private static (string, string, string, string) Key(ProjectModule module) => (module.Root, module.TechnologyId, module.Origin, module.Name);

    private static VersionChange Change(ProjectModule module, string kind, string? from, string? to) =>
        new(module.Root, module.TechnologyId, module.Name, module.Origin, kind, from, to, module.Evidence);

    private static string Guidance(TechnologyProfile? baseline, IReadOnlyList<VersionChange> changes, IReadOnlyList<ModuleCompatibility> incompatible)
    {
        var parts = new List<string>();
        if (baseline is null || baseline.Modules.Count == 0)
            parts.Add("Sem perfil com versões por pasta no Spock: não há com o que comparar. Analise o projeto no Spock para ter a referência.");
        else if (changes.Count == 0)
            parts.Add($"As versões do projeto continuam as do perfil de {baseline.AnalyzedAt:yyyy-MM-dd}.");
        else
            parts.Add("Versões mudaram desde a análise do Spock. Se a tarefa não pediu essa mudança, desfaça: atualizar versão é uma " +
                      "modernização à parte, com plano aprovado. Se pediu, diga na entrega o que mudou e por quê.");
        if (incompatible.Count > 0)
            parts.Add("Há combinação de versões incompatível no estado atual do projeto; veja o motivo de cada uma antes de concluir.");
        return string.Join(" ", parts);
    }
}
