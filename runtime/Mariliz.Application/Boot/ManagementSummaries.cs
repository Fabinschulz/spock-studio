using System.Text.Json.Nodes;
using static Mariliz.Application.Boot.SummaryJson;

namespace Mariliz.Application.Boot;

/// <summary>
/// Resumos da gestão: indicadores, previsão, marcos, relatório, decisões e histórico de riscos. Os números e as datas são os do motor;
/// nomes de marcos, títulos de itens e justificativas nunca entram.
/// </summary>
internal static class ManagementSummaries
{
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal) { ["issues"] = "Issues", ["pullRequests"] = "Pull requests" };

    private static readonly Dictionary<string, string> Confidence = new(StringComparer.Ordinal) { ["low"] = "baixa", ["medium"] = "média", ["high"] = "alta" };

    private static readonly (string Outlook, string One, string Many)[] Outlooks =
    [
        ("on_track", "no prazo", "no prazo"), ("at_risk", "em risco", "em risco"), ("behind", "atrasado", "atrasados"), ("overdue", "vencido", "vencidos"),
        ("done", "concluído", "concluídos"), ("no_due_date", "sem prazo", "sem prazo"), ("no_forecast", "sem previsão", "sem previsão")
    ];

    private const string PartialSync = "A última sincronização foi parcial: o resultado pode estar incompleto.";

    public static CapabilitySummary Tracking(JsonObject result)
    {
        if (!HasList(result, "kinds")) return NoData("dos indicadores de fluxo");
        var kinds = Array(result, "kinds");
        if (kinds.Count == 0) return new CapabilitySummary("Sem dados para calcular indicadores.", ["As integrações do projeto ainda não trouxeram itens."]);

        var evidence = kinds.Select(Flow).ToList();
        var notCalculated = Size(result, "notCalculated");
        if (notCalculated > 0) evidence.Add($"{Plural(notCalculated, "indicador ainda não calculado", "indicadores ainda não calculados")}; a tela diz o motivo de cada um.");
        if (Flag(result, "complete") == false) evidence.Add(PartialSync);
        var weeks = Number(result, "periodWeeks") is { } w ? $" nas últimas {Plural(w, "semana", "semanas")}" : "";
        return new CapabilitySummary($"Indicadores de fluxo de {Plural(kinds.Count, "tipo de dado", "tipos de dado")}{weeks}.", evidence);
    }

    public static CapabilitySummary Forecast(JsonObject result)
    {
        if (!HasList(result, "kinds")) return NoData("da previsão de entrega");
        var kinds = Array(result, "kinds");
        var forecasts = kinds.Where(kind => Size(kind, "scenarios") > 0).ToList();
        var evidence = kinds.Select(Projected).ToList();
        if (Flag(result, "complete") == false) evidence.Add(PartialSync);
        return new CapabilitySummary(forecasts.Count == 0 ? "Nenhuma previsão calculada." : $"{Plural(forecasts.Count, "previsão calculada", "previsões calculadas")}.", evidence);
    }

    public static CapabilitySummary Milestones(JsonObject result)
    {
        if (!HasList(result, "milestones")) return NoData("dos marcos");
        var milestones = Array(result, "milestones");
        var evidence = new List<string>();
        var outlooks = Outlooks.Select(item => (Count: milestones.Count(milestone => Text(milestone, "outlook") == item.Outlook), item.One, item.Many))
            .Where(item => item.Count > 0).Select(item => Plural(item.Count, item.One, item.Many)).ToList();
        if (outlooks.Count > 0) evidence.Add($"Situação: {string.Join(", ", outlooks)}.");
        if (milestones.Count > 0)
            evidence.Add($"{Plural(milestones.Sum(item => Number(item, "delivered") ?? 0), "item entregue", "itens entregues")} e {milestones.Sum(item => Number(item, "open") ?? 0)} em aberto nos marcos.");
        var unassigned = Array(result, "unassigned").Sum(item => Number(item, "items") ?? 0);
        if (unassigned > 0) evidence.Add($"{Plural(unassigned, "item sem marco", "itens sem marco")}.");
        if (Number(result, "omitted") is > 0 and var omitted) evidence.Add($"{Plural(omitted, "marco ficou", "marcos ficaram")} de fora da resposta.");
        if (Flag(result, "complete") == false) evidence.Add(PartialSync);
        return new CapabilitySummary(milestones.Count == 0 ? "Nenhum marco encontrado nas fontes do projeto." : $"{Plural(milestones.Count, "marco", "marcos")} no projeto.", evidence);
    }

    public static CapabilitySummary Report(JsonObject result)
    {
        if (Object(result, "metrics") is null && Object(result, "risks") is null && Object(result, "milestones") is null) return NoData("do relatório do período");
        var risks = Object(result, "risks");
        var forecasts = Array(Object(result, "forecast"), "kinds");
        var runs = Array(result, "runs").Sum(item => Number(item, "count") ?? 0);
        var metrics = Object(result, "metrics");
        var milestones = Object(result, "milestones");
        var evidence = new List<string>
        {
            $"Tipos de dado com indicadores: {Listed(metrics, "kinds")}.",
            $"Tipos de dado com previsão: {forecasts.Count(kind => Size(kind, "scenarios") > 0)} de {forecasts.Count}.",
            Flag(risks, "hasData") == false ? "Riscos: sem dado." : $"Riscos encontrados: {Listed(risks, "risks")}.",
            $"Mudanças de risco no período: {Listed(result, "riskChanges")}.",
            $"Marcos: {Listed(milestones, "milestones")}.",
            $"Decisões: {Listed(result, "decisions")}.",
            $"Execuções: {(HasList(result, "runs") ? runs.ToString() : NoValue)}."
        };
        return new CapabilitySummary(Number(result, "weeks") is { } weeks ? $"Relatório das últimas {Plural(weeks, "semana", "semanas")}." : "Relatório do período.", evidence);
    }

    public static CapabilitySummary Decisions(JsonObject result)
    {
        if (!HasList(result, "decisions")) return NoData("das decisões registradas");
        var decisions = Array(result, "decisions");
        if (decisions.Count == 0) return new CapabilitySummary("Nenhuma decisão registrada neste projeto.", []);

        var evidence = new List<string>();
        var latest = decisions.Select(decision => Day(decision, "recordedAt")).Where(day => day is not null).OrderByDescending(day => day, StringComparer.Ordinal).FirstOrDefault();
        evidence.Add(latest is null ? $"Data da decisão mais recente: {NoValue}." : $"A mais recente foi registrada em {latest}.");
        var withFacts = decisions.Count(decision => (Number(decision, "factCount") ?? 0) > 0);
        if (withFacts > 0) evidence.Add($"{Plural(withFacts, "decisão guarda", "decisões guardam")} o retrato dos fatos do momento.");
        return new CapabilitySummary($"{Plural(decisions.Count, "decisão registrada", "decisões registradas")}.", evidence);
    }

    public static CapabilitySummary RiskHistory(JsonObject result)
    {
        if (!HasList(result, "trails")) return NoData("do histórico de riscos");
        var trails = Array(result, "trails");
        var snapshots = Number(result, "snapshots");
        if (trails.Count == 0)
            return new CapabilitySummary("Nenhum risco no histórico.", [$"Registros de sincronização no período: {Count(snapshots)}."]);

        var active = trails.Where(trail => Flag(trail, "active") == true).ToList();
        var changes = trails.SelectMany(trail => Array(trail, "changes")).Select(change => Text(change, "kind")).ToList();
        var evidence = new List<string>();
        var severities = new[] { (Id: "high", Label: "alta"), (Id: "medium", Label: "média"), (Id: "low", Label: "baixa") }
            .Select(item => (item.Label, Count: active.Count(trail => Text(trail, "currentSeverity") == item.Id))).Where(item => item.Count > 0)
            .Select(item => $"{item.Count} de gravidade {item.Label}").ToList();
        if (severities.Count > 0) evidence.Add($"Ativos agora: {string.Join(", ", severities)}.");
        evidence.Add($"{Plural(changes.Count(kind => kind == "raised"), "aumento", "aumentos")} e {Plural(changes.Count(kind => kind == "lowered"), "redução", "reduções")} de gravidade no período.");
        evidence.Add($"Registros de sincronização no período: {Count(snapshots)}{(Day(result, "since") is { } since ? $", desde {since}" : "")}.");
        if (Number(result, "omitted") is > 0 and var omitted) evidence.Add($"{Plural(omitted, "risco ficou", "riscos ficaram")} de fora da resposta.");
        return new CapabilitySummary($"{Plural(trails.Count, "risco acompanhado", "riscos acompanhados")}: {active.Count} ativos e {trails.Count - active.Count} resolvidos.", evidence);
    }

    private static string Flow(JsonObject kind)
    {
        var throughput = Object(kind, "throughput");
        var open = Number(kind, "open");
        var items = Number(kind, "items");
        var load = open is { } openCount && items is { } total ? $"{Plural(openCount, "aberto", "abertos")} de {total}" : $"abertos: {Count(open)}, itens no total: {Count(items)}";
        var average = Decimal(throughput, "weeklyAverage");
        var delivered = $"entregues: {Count(Number(throughput, "total"))}{(average is null ? "" : $" ({Fixed(average)} por semana)")}";
        return $"{Label(Text(kind, "kind"), Kinds)}: {load}; em andamento: {Count(Number(Object(kind, "wip"), "value"))}; {delivered}; "
            + $"lead time mediano: {Days(Decimal(Object(kind, "leadTime"), "medianDays"))}; cycle time mediano: {Days(Decimal(Object(kind, "cycleTime"), "medianDays"))}; "
            + $"bloqueados: {Count(Number(Object(kind, "blocked"), "value"))}.";
    }

    private static string Projected(JsonObject kind)
    {
        var label = Label(Text(kind, "kind"), Kinds);
        var scenarios = Array(kind, "scenarios");
        if (scenarios.Count == 0)
            return $"{label}: sem previsão{(Number(kind, "historyWeeks") is { } history ? $" ({Plural(history, "semana", "semanas")} de histórico)" : "")}.";

        JsonObject? Scenario(string id) => scenarios.FirstOrDefault(item => Text(item, "id") == id);
        var likelyWeeks = Number(Scenario("likely"), "weeks");
        var by = Text(Scenario("likely"), "by");
        var likely = likelyWeeks is { } weeks ? $"provável em {Plural(weeks, "semana", "semanas")}{(by is null ? "" : $" (até {by})")}" : $"provável: {NoValue}";
        var range = Number(Scenario("optimistic"), "weeks") is { } low && Number(Scenario("conservative"), "weeks") is { } high
            ? $"entre {low} e {high} semanas nos cenários otimista e conservador"
            : $"cenários otimista e conservador: {NoValue}";
        return $"{label}: restam {Count(Number(kind, "remaining"))}; {likely}; {range}; confiança {Label(Text(kind, "confidence"), Confidence)}.";
    }

    private static string Listed(JsonObject? node, string name) => HasList(node, name) ? Size(node, name).ToString() : NoValue;
}
