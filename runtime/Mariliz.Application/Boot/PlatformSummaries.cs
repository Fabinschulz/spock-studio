using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>Resumos da linha do tempo e da trilha de auditoria: contagens por tipo. Títulos e detalhes dos eventos ficam na tela.</summary>
internal static class PlatformSummaries
{
    private static readonly (string Id, string Label)[] TimelineKinds =
    [
        ("harness", "Harness"), ("profile", "perfil"), ("run", "execuções"), ("decision", "decisões"), ("risk", "riscos"), ("sync", "sincronizações")
    ];

    private static readonly (string Id, string Label)[] AuditCategories =
    [
        ("change", "mudanças no projeto"), ("access", "acesso a fontes"), ("read", "leituras de fontes"), ("credential", "conexões"),
        ("decision", "decisões"), ("context", "contexto de IA"), ("model", "uso de modelo")
    ];

    public static CapabilitySummary Timeline(JsonObject result)
    {
        if (!HasList(result, "events")) return NoData("da linha do tempo");
        var events = Array(result, "events");
        if (events.Count == 0) return new CapabilitySummary("Nenhum evento recente no projeto.", []);

        var evidence = new List<string> { "Por tipo: " + Breakdown(events, "kind", TimelineKinds) + "." };
        var attention = events.Count(item => Text(item, "level") is "attention" or "problem");
        if (attention > 0) evidence.Add($"{Plural(attention, "evento pede atenção ou teve problema", "eventos pedem atenção ou tiveram problema")}.");
        Tail(result, evidence, "eventos");
        return new CapabilitySummary($"{Plural(events.Count, "evento recente", "eventos recentes")} no projeto.", evidence);
    }

    public static CapabilitySummary Audit(JsonObject result)
    {
        if (!HasList(result, "entries")) return NoData("da trilha de auditoria");
        var entries = Array(result, "entries");
        if (entries.Count == 0) return new CapabilitySummary("Nenhum registro de auditoria recente.", []);

        var evidence = new List<string> { "Por categoria: " + Breakdown(entries, "category", AuditCategories) + "." };
        Tail(result, evidence, "registros");
        return new CapabilitySummary($"{Plural(entries.Count, "registro de auditoria recente", "registros de auditoria recentes")}.", evidence);
    }

    private static string Breakdown(List<JsonObject> items, string field, (string Id, string Label)[] known)
    {
        var parts = known.Select(item => (item.Label, Count: items.Count(entry => Text(entry, field) == item.Id))).Where(item => item.Count > 0)
            .Select(item => $"{item.Label} {item.Count}").ToList();
        var others = items.Count(entry => known.All(item => item.Id != Text(entry, field)));
        if (others > 0) parts.Add($"outros {others}");
        return string.Join(", ", parts);
    }

    private static void Tail(JsonObject result, List<string> evidence, string noun)
    {
        if (Text(result, "nextBefore") is not null) evidence.Add($"Há {noun} mais antigos além desta página.");
        var unavailable = Array(result, "sources").Count(source => Flag(source, "available") == false);
        if (unavailable > 0) evidence.Add($"{Plural(unavailable, "fonte indisponível", "fontes indisponíveis")}: o resultado pode estar incompleto.");
    }
}
