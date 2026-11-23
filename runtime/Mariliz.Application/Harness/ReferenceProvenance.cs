using System.Globalization;
using Mariliz.Application.Workflows;
using Mariliz.Core.Text;

namespace Mariliz.Application.Harness;

/// <param name="Patterns">"REF-A1:1a2b3c4d": o id e o começo do hash do texto do padrão (título e colunas) quando a regra foi gerada.</param>
public sealed record ReferenceProvenance(string Source, string AnalyzedAt, IReadOnlyList<string> Patterns);

// W3-3: uma regra gerada da pasta Organização guarda no lock de onde veio. Com isso o plano diz o que mudou numa revisão do
// catálogo, e o estado do Harness aponta a regra cuja referência ficou para trás, mesmo com o arquivo intacto no disco.
public static class ReferenceProvenances
{
    private const int HashLength = 8;

    public static ReferenceProvenance Of(IReadOnlyList<ReferencePattern> patterns) => new(
        patterns.Count == 0 ? "" : patterns[0].Source,
        patterns.Count == 0 ? "" : Date(patterns[0].AnalyzedAt),
        patterns.Select(pattern => $"{pattern.Id}:{Hash(pattern)}").ToList());

    // O que separa a regra gravada da que o catálogo gera hoje, em frases curtas; vazio quando nada mudou.
    public static IReadOnlyList<string> Changes(ReferenceProvenance before, ReferenceProvenance after)
    {
        var old = Split(before);
        var current = Split(after);
        var changes = new List<string>();
        if (before.AnalyzedAt != after.AnalyzedAt) changes.Add($"análise de {before.AnalyzedAt} para {after.AnalyzedAt}");
        Describe(changes, "novos", current.Keys.Where(id => !old.ContainsKey(id)));
        Describe(changes, "removidos", old.Keys.Where(id => !current.ContainsKey(id)));
        Describe(changes, "com texto novo", current.Keys.Where(id => old.TryGetValue(id, out var hash) && hash != current[id]));
        return changes;
    }

    // Contra o catálogo deste motor, só os padrões que a regra já tem: quais entrariam depende da stack, que o estado não descobre.
    public static IReadOnlyList<string> Staleness(ReferenceProvenance recorded)
    {
        var catalog = OrganizationReferences.Patterns.ToDictionary(pattern => pattern.Id, StringComparer.Ordinal);
        var present = Split(recorded).Keys.Where(catalog.ContainsKey).Select(id => catalog[id]).ToList();
        var current = new ReferenceProvenance(
            present.FirstOrDefault()?.Source ?? recorded.Source,
            present.FirstOrDefault() is { } first ? Date(first.AnalyzedAt) : recorded.AnalyzedAt,
            present.Select(pattern => $"{pattern.Id}:{Hash(pattern)}").ToList());
        return Changes(recorded, current);
    }

    private static Dictionary<string, string> Split(ReferenceProvenance provenance) =>
        provenance.Patterns
            .Select(item => item.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First()[1], StringComparer.Ordinal);

    private static void Describe(List<string> changes, string what, IEnumerable<string> ids)
    {
        var list = ids.Order(StringComparer.Ordinal).ToList();
        if (list.Count > 0) changes.Add($"padrões {what}: {string.Join(", ", list)}");
    }

    private static string Hash(ReferencePattern pattern) =>
        Hashing.Sha256($"{pattern.Title}\n{string.Join("\n", pattern.Details.Select(detail => $"{detail.Label}: {detail.Text}"))}")[..HashLength];

    private static string Date(DateTimeOffset value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
