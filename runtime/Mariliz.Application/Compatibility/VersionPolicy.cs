using System.Globalization;

namespace Mariliz.Application.Compatibility;

public static class VersionGoals
{
    // Sem preferência do usuário: a LTS ativa mais nova (docs/compatibilidade-multiversao/POLITICA.md, seção 2).
    public const string Production = "production";
    public const string Latest = "latest";

    public static readonly IReadOnlyList<string> All = [Production, Latest];
}

/// <param name="Version">A mais nova da linha; nula quando a fonte só publica a linha (Python).</param>
/// <param name="Language">Versão padrão da linguagem que a linha define (C# por .NET).</param>
public sealed record VersionChoice(string Cycle, string Channel, string? Version, string? Eol, string? Language, string Reason);

/// <param name="Choice">Nula quando a tecnologia está fora do catálogo ou nenhuma linha cumpre a política.</param>
/// <param name="Alternatives">As outras linhas que a política aceita, com o motivo de cada uma; a escolha fica com o usuário.</param>
/// <param name="Notes">O que pesa na decisão sem mudar a escolha: data de fim ausente, linha recém-lançada, requisito entre tecnologias.</param>
public sealed record VersionRecommendation(
    string TechnologyId,
    string Name,
    string? Source,
    DateTimeOffset? CheckedAt,
    bool Stale,
    VersionChoice? Choice,
    IReadOnlyList<VersionChoice> Alternatives,
    IReadOnlyList<string> Notes);

public sealed record VersionRecommendationReport(DateTimeOffset CatalogGeneratedAt, string Goal, IReadOnlyList<VersionRecommendation> Technologies);

// MV-5: as versões de um projeto novo, escolhidas juntas e só com dados do catálogo embutido. Mesmo pedido, mesmo catálogo e
// mesma data dão a mesma resposta; nada aqui consulta a rede nem olha a máquina.
public static class VersionPolicy
{
    public const int SupportMarginMonths = 12;
    private const int RecentReleaseDays = 90;
    private const string Node = "node";

    public static VersionRecommendationReport Recommend(IReadOnlyList<string> technologies, string goal, ReleaseCatalog catalog, DateOnly today)
    {
        if (!VersionGoals.All.Contains(goal))
            throw new ArgumentException($"Objetivo desconhecido: {goal}. Use {string.Join(" ou ", VersionGoals.All)}.", nameof(goal));
        var ids = technologies.Select(id => id.Trim().ToLowerInvariant()).Where(id => id.Length > 0).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("Informe ao menos uma tecnologia.", nameof(technologies));

        // O Node vem primeiro: os pacotes do npm declaram o Node que exigem, e a escolha deles depende dele.
        var node = ids.Contains(Node) && catalog.Find(Node) is { } nodeTechnology ? Choose(nodeTechnology, goal, today, null) : null;
        var recommendations = ids.Select(id => id == Node && node is not null
                ? node
                : catalog.Find(id) is { } technology
                    ? Choose(technology, goal, today, node?.Choice)
                    : new VersionRecommendation(id, id, null, null, false, null, [],
                        ["Fora do catálogo de versões: sem fonte oficial, o Spock não recomenda versão."]))
            .ToList();
        return new VersionRecommendationReport(catalog.GeneratedAt, goal, recommendations);
    }

    private static VersionRecommendation Choose(CatalogTechnology technology, string goal, DateOnly today, VersionChoice? node)
    {
        var notes = new List<string>();
        if (technology.Stale) notes.Add($"A última consulta à fonte falhou; as linhas são as de {technology.CheckedAt:yyyy-MM-dd}.");

        var eligible = new List<ReleaseLine>();
        foreach (var line in technology.Lines.Where(line => line.Status == "active" && line.Channel != "preview"))
        {
            if (CompatibilityAssessor.EndOfLife(line.Eol) is { } end && end < today.AddMonths(SupportMarginMonths))
                notes.Add($"{technology.Name} {line.Cycle} fica de fora: o suporte termina em {end:yyyy-MM-dd}, a menos de {SupportMarginMonths} meses.");
            else eligible.Add(line);
        }

        var ordered = Ordered(eligible, goal);
        if (ordered.Count == 0)
        {
            notes.Add($"Nenhuma linha estável de {technology.Name} está ativa com {SupportMarginMonths} meses de suporte pela frente.");
            return Recommendation(technology, null, [], notes);
        }

        var chosen = ordered[0];
        if ((node?.Version ?? node?.Cycle) is { } nodeVersion && technology.Id != Node)
            chosen = WithNode(technology, ordered, nodeVersion, notes);
        else if (technology.Id != Node && chosen.Requires?.GetValueOrDefault(Node) is { } range)
            notes.Add($"{technology.Name} {chosen.Cycle} exige Node {range}.");

        if (chosen.Eol is null)
            notes.Add(technology.Policy == "vendor"
                ? $"O fim de suporte de {technology.Name} {chosen.Cycle} depende do fornecedor da distribuição."
                : $"A fonte de {technology.Name} não publica data de fim de suporte por linha.");
        if (DateOnly.TryParseExact(chosen.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var released)
            && today.DayNumber - released.DayNumber < RecentReleaseDays)
            notes.Add($"{technology.Name} {chosen.Cycle} saiu em {released:yyyy-MM-dd}: confira se as dependências do projeto já a suportam.");

        var choice = Line(chosen, Reason(technology, chosen, eligible, goal));
        var alternatives = Alternatives(technology, chosen, ordered, goal);
        return Recommendation(technology, choice, alternatives, notes);
    }

    // Em produção, as LTS vêm antes; entre linhas do mesmo tipo, a mais nova primeiro.
    private static List<ReleaseLine> Ordered(List<ReleaseLine> eligible, string goal) =>
        eligible
            .OrderBy(line => goal == VersionGoals.Production && line.Channel != "lts" ? 1 : 0)
            .ThenByDescending(line => line.Cycle, Comparer<string>.Create(VersionCycles.Compare))
            .ToList();

    // A linha preferida que aceita o Node escolhido; uma mais antiga só entra quando a preferida não aceita.
    private static ReleaseLine WithNode(CatalogTechnology technology, List<ReleaseLine> ordered, string nodeVersion, List<string> notes)
    {
        var preferred = ordered[0];
        foreach (var line in ordered)
        {
            if (line.Requires?.GetValueOrDefault(Node) is not { } range) return line;
            switch (NodeRange.Evaluate(range, nodeVersion))
            {
                case RangeVerdict.Satisfied:
                    if (line != preferred)
                        notes.Add($"{technology.Name} {preferred.Cycle} exige Node {preferred.Requires![Node]}, que o Node {nodeVersion} escolhido não cumpre; " +
                                  $"a linha {line.Cycle} aceita.");
                    return line;
                case RangeVerdict.Unknown:
                    notes.Add($"{technology.Name} {line.Cycle} exige Node {range}: confira com o Node {nodeVersion} escolhido.");
                    return line;
            }
        }
        notes.Add($"Nenhuma linha ativa de {technology.Name} aceita o Node {nodeVersion} escolhido; a combinação precisa de outro Node.");
        return preferred;
    }

    private static string Reason(CatalogTechnology technology, ReleaseLine chosen, List<ReleaseLine> eligible, string goal)
    {
        var until = chosen.Eol is { } eol ? $", com suporte até {eol}" : "";
        if (goal == VersionGoals.Production && chosen.Channel == "lts")
        {
            var tie = eligible.FirstOrDefault(line => line.Channel != "lts" && VersionCycles.Compare(line.Cycle, chosen.Cycle) > 0 && line.Eol == chosen.Eol && line.Eol is not null);
            return $"LTS ativa mais nova{until}." + (tie is null ? ""
                : $" Empata em fim de suporte com a {tie.Cycle} ({tie.Channel.ToUpperInvariant()}); a LTS ganha porque é o caminho oficial para a próxima LTS.");
        }
        return technology.Policy switch
        {
            "latest-major" => $"Linha mais nova: só a maior mais recente de {technology.Name} recebe correções garantidas{until}.",
            "rolling" => $"Linha mais nova: {technology.Name} corrige só a estável mais recente.",
            "supported-window" => $"Linha suportada mais nova{until}.",
            _ => $"Linha estável ativa mais nova{until}."
        };
    }

    private static List<VersionChoice> Alternatives(CatalogTechnology technology, ReleaseLine chosen, List<ReleaseLine> ordered, string goal)
    {
        var alternatives = new List<VersionChoice>();
        foreach (var line in ordered.Where(line => line != chosen))
        {
            var newer = VersionCycles.Compare(line.Cycle, chosen.Cycle) > 0;
            var until = line.Eol is { } eol ? $", suporte até {eol}" : "";
            if (newer)
                alternatives.Add(Line(line, $"Mais nova que a recomendada ({line.Channel.ToUpperInvariant()}{until})."));
            else if (goal == VersionGoals.Latest && line.Channel == "lts" && chosen.Channel != "lts")
                alternatives.Add(Line(line, $"LTS ativa mais nova{until}: a escolha para produção."));
        }
        return alternatives;
    }

    private static VersionChoice Line(ReleaseLine line, string reason) =>
        new(line.Cycle, line.Channel, line.Latest, line.Eol, line.Language, reason);

    private static VersionRecommendation Recommendation(CatalogTechnology technology, VersionChoice? choice, List<VersionChoice> alternatives, List<string> notes) =>
        new(technology.Id, technology.Name, technology.Source, technology.CheckedAt, technology.Stale, choice, alternatives, notes);
}
