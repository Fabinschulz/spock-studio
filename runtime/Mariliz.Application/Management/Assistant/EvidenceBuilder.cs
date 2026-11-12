using System.Globalization;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management.Assistant;

/// <summary>
/// Escreve os fatos que o assistente pode citar, a partir dos indicadores, da previsão e dos riscos já calculados. Os
/// textos levam os números como o motor os calculou, para que a verificação depois consiga conferir cada um.
/// Títulos de itens só entram quando todas as fontes do projeto autorizaram servir de contexto.
/// </summary>
public static class EvidenceBuilder
{
    public static EvidencePack Build(
        string projectId,
        string projectName,
        DateTimeOffset now,
        IReadOnlyList<ProjectIntegration> integrations,
        ProjectMetrics metrics,
        ProjectForecast forecast,
        ProjectRisks risks)
    {
        var titlesAllowed = integrations.Count > 0 && integrations.All(integration => integration.Policy.FeedsKnowledge);
        var facts = new List<EvidenceFact>
        {
            new("today", "contexto", $"Hoje é {Day(now)}. Período dos indicadores: últimas {metrics.PeriodWeeks} semanas.")
        };

        foreach (var (source, index) in metrics.Sources.Select((source, index) => (source, index + 1)))
            facts.Add(new($"src.{index}", "fontes",
                $"Fonte {source.Provider} {source.ResourceName}: última sincronização {Status(source.LastSyncStatus)}"
                + (source.LastSyncedAt is { } at ? $", dados lidos em {Day(at)}." : ", ainda sem dados lidos.")));
        if (metrics.Sources.Count == 0) facts.Add(new("src.none", "fontes", "Nenhuma ferramenta vinculada ao projeto."));

        foreach (var flow in metrics.Kinds) facts.AddRange(Flow(flow));
        foreach (var kind in forecast.Kinds) facts.AddRange(Forecast(kind));
        facts.AddRange(Risks(risks, titlesAllowed));
        foreach (var missing in metrics.NotCalculated)
            facts.Add(new($"missing.{missing.Id}", "não calculado", $"{missing.Label} não é calculado: {missing.Reason}"));

        var withheld = new List<string> { "Nenhum código, documento ou arquivo do projeto é enviado." };
        withheld.Add(titlesAllowed
            ? "Títulos de itens citados em riscos foram incluídos: todas as fontes autorizaram servir de contexto."
            : "Títulos de itens não foram incluídos: nem todas as fontes autorizaram servir de contexto; vão só os identificadores.");
        return new EvidencePack(projectId, projectName, now, facts, withheld);
    }

    private static IEnumerable<EvidenceFact> Flow(KindFlow flow)
    {
        var id = $"flow.{flow.Kind}";
        var label = KindLabel(flow.Kind);
        yield return new($"{id}.items", "indicadores", $"{label}: {flow.Items} itens sincronizados, {flow.Open} abertos.");
        yield return Metric($"{id}.wip", $"{label} em andamento agora", flow.Wip.Value?.ToString(CultureInfo.InvariantCulture), flow.Wip.Unavailable);
        if (flow.Throughput.Unavailable is { } throughputMissing)
            yield return new($"{id}.throughput", "indicadores", $"{label} entregues por semana: sem valor ({throughputMissing})");
        else
            yield return new($"{id}.throughput", "indicadores",
                $"{label} entregues por semana, da mais antiga para a mais recente: {string.Join(", ", flow.Throughput.Weeks.Select(w => w.Delivered))}; "
                + $"total {flow.Throughput.Total}, média {Number(flow.Throughput.WeeklyAverage!.Value)} por semana. "
                + $"Itens novos por semana: {string.Join(", ", flow.Throughput.Weeks.Select(w => w.Created))}.");
        yield return flow.LeadTime.Unavailable is { } leadMissing
            ? new($"{id}.leadtime", "indicadores", $"Lead time de {label}: sem valor ({leadMissing})")
            : new($"{id}.leadtime", "indicadores",
                $"Lead time de {label}: mediana {Number(flow.LeadTime.MedianDays!.Value)} dias, percentil 85 {Number(flow.LeadTime.P85Days!.Value)} dias, sobre {flow.LeadTime.Sample} entregas.");
        yield return flow.CycleTime.Unavailable is { } cycleMissing
            ? new($"{id}.cycletime", "indicadores", $"Cycle time de {label}: sem valor ({cycleMissing})")
            : new($"{id}.cycletime", "indicadores",
                $"Cycle time de {label}: mediana {Number(flow.CycleTime.MedianDays!.Value)} dias, percentil 85 {Number(flow.CycleTime.P85Days!.Value)} dias, "
                + $"sobre {flow.CycleTime.Sample} entregas com início conhecido ({flow.CycleTime.Excluded} sem início conhecido).");
        yield return Metric($"{id}.overdue", $"{label} abertos com prazo vencido", flow.Overdue.Value?.ToString(CultureInfo.InvariantCulture), flow.Overdue.Unavailable);
    }

    private static IEnumerable<EvidenceFact> Forecast(DeliveryForecast forecast)
    {
        var id = $"forecast.{forecast.Kind}";
        var label = KindLabel(forecast.Kind);
        if (forecast.Unavailable is { } reason)
        {
            yield return new($"{id}", "previsão", $"Previsão de {label}: indisponível. {reason}");
            yield break;
        }
        yield return new($"{id}", "previsão",
            $"Previsão de {label} para {forecast.Remaining} itens abertos, por simulação sobre {forecast.HistoryWeeks} semanas de histórico: "
            + string.Join("; ", forecast.Scenarios.Select(s => $"{s.Label.ToLowerInvariant()} (percentil {s.Percentile}) em {s.Weeks} semanas, até {s.By:yyyy-MM-dd}"))
            + ".");
        yield return new($"{id}.confidence", "previsão",
            $"Confiança da previsão de {label}: {Confidence(forecast.Confidence!.Value)}."
            + (forecast.ConfidenceReasons.Count > 0 ? " Motivos: " + string.Join(" ", forecast.ConfidenceReasons) : ""));
        foreach (var (factor, index) in forecast.RiskFactors.Select((factor, index) => (factor, index + 1)))
            yield return new($"{id}.factor.{index}", "previsão", $"Fator que pode atrasar {label}: {factor}");
    }

    private static IEnumerable<EvidenceFact> Risks(ProjectRisks risks, bool titlesAllowed)
    {
        if (!risks.HasData) yield break;
        if (risks.Risks.Count == 0)
            yield return new("risk.none", "riscos", $"Nenhuma das {risks.RulesEvaluated.Count} regras de risco disparou.");
        foreach (var risk in risks.Risks)
        {
            var items = risk.Items.Count == 0
                ? ""
                : " Itens: " + string.Join(", ", risk.Items.Select(item => titlesAllowed ? $"{item.ExternalId} ({item.Title})" : item.ExternalId)) + ".";
            yield return new($"risk.{risk.Id}", "riscos",
                $"Risco detectado por regra: {risk.Title}, severidade {Severity(risk.Severity)}. {string.Join(" ", risk.Evidence)}"
                + (risk.Trend is { } trend ? $" Tendência: {trend}" : "") + items);
        }
    }

    private static EvidenceFact Metric(string id, string label, string? value, string? unavailable) =>
        new(id, "indicadores", value is null ? $"{label}: sem valor ({unavailable})" : $"{label}: {value}.");

    private static string KindLabel(string kind) => kind switch
    {
        DataKinds.Issues => "Issues",
        DataKinds.PullRequests => "Pull requests",
        _ => kind
    };

    private static string Status(SyncStatus? status) => status switch
    {
        SyncStatus.Succeeded => "concluída",
        SyncStatus.Partial => "parcial",
        SyncStatus.Failed => "com falha",
        SyncStatus.Running => "em andamento",
        _ => "nunca feita"
    };

    private static string Confidence(ForecastConfidence confidence) => confidence switch
    {
        ForecastConfidence.High => "alta",
        ForecastConfidence.Medium => "média",
        _ => "baixa"
    };

    private static string Severity(RiskSeverity severity) => severity switch
    {
        RiskSeverity.High => "alta",
        RiskSeverity.Medium => "média",
        _ => "baixa"
    };

    private static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
