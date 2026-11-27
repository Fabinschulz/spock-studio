using System.Globalization;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management.Assistant;

// Os textos levam os números como o motor os calculou, para a verificação conseguir conferir cada um. Títulos de itens só entram quando todas as fontes do projeto autorizaram servir de contexto.
public static class EvidenceBuilder
{
    public static EvidencePack Build(
        string projectId,
        string projectName,
        DateTimeOffset now,
        IReadOnlyList<ProjectIntegration> integrations,
        ProjectMetrics metrics,
        ProjectForecast forecast,
        ProjectRisks risks,
        ProjectMilestones milestones)
    {
        var titlesAllowed = integrations.Count > 0 && integrations.All(integration => integration.Policy.FeedsKnowledge);
        var facts = new List<EvidenceFact>
        {
            new("today", FactGroups.Context, $"Hoje é {Day(now)}. Período dos indicadores: últimas {metrics.PeriodWeeks} semanas.")
        };

        foreach (var (source, index) in metrics.Sources.Select((source, index) => (source, index + 1)))
            facts.Add(new($"src.{index}", FactGroups.Sources,
                $"Fonte {source.Provider} {source.ResourceName}: última sincronização {Status(source.LastSyncStatus)}"
                + (source.LastSyncedAt is { } at ? $", dados lidos em {Day(at)}." : ", ainda sem dados lidos.")));
        if (metrics.Sources.Count == 0) facts.Add(new("src.none", FactGroups.Sources, "Nenhuma ferramenta vinculada ao projeto."));

        foreach (var flow in metrics.Kinds) facts.AddRange(Flow(flow));
        foreach (var kind in forecast.Kinds) facts.AddRange(Forecast(kind));
        facts.AddRange(Milestones(milestones));
        facts.AddRange(Risks(risks, titlesAllowed));
        foreach (var missing in metrics.NotCalculated)
            facts.Add(new($"missing.{missing.Id}", FactGroups.NotCalculated, $"{missing.Label}: ainda sem cálculo. {missing.Reason}"));

        var withheld = new List<string> { "Nenhum código, documento ou arquivo do projeto entra nos fatos." };
        // Sem fonte não há item, e falar de títulos retidos só confunde.
        if (integrations.Count > 0)
            withheld.Add(titlesAllowed
                ? "Títulos de itens citados em riscos foram incluídos: todas as fontes autorizaram servir de contexto."
                : "Títulos de itens não foram incluídos: nem todas as fontes autorizaram servir de contexto; vão só os identificadores.");
        return new EvidencePack(projectId, projectName, now, facts, withheld);
    }

    private static IEnumerable<EvidenceFact> Flow(KindFlow flow)
    {
        var id = $"flow.{flow.Kind}";
        var label = KindLabel(flow.Kind);
        yield return new($"{id}.items", FactGroups.Flow, $"{label}: {flow.Items} itens sincronizados, {flow.Open} abertos.");
        yield return Metric($"{id}.wip", $"{label} em andamento agora", flow.Wip.Value?.ToString(CultureInfo.InvariantCulture), flow.Wip.Unavailable);
        if (flow.Throughput.Unavailable is { } throughputMissing)
            yield return new($"{id}.throughput", FactGroups.Flow, $"{label} entregues por semana: sem valor ({throughputMissing})");
        else
            yield return new($"{id}.throughput", FactGroups.Flow,
                $"{label} entregues por semana nas últimas {flow.Throughput.Weeks.Count()} semanas, da mais antiga para a mais recente: {string.Join(", ", flow.Throughput.Weeks.Select(w => w.Delivered))}; "
                + $"total {flow.Throughput.Total}, média {Number(flow.Throughput.WeeklyAverage!.Value)} por semana. "
                + $"Itens novos por semana: {string.Join(", ", flow.Throughput.Weeks.Select(w => w.Created))}.");
        yield return flow.LeadTime.Unavailable is { } leadMissing
            ? new($"{id}.leadtime", FactGroups.Flow, $"Lead time de {label}: sem valor ({leadMissing})")
            : new($"{id}.leadtime", FactGroups.Flow,
                $"Lead time de {label}: mediana {Number(flow.LeadTime.MedianDays!.Value)} dias, percentil 85 {Number(flow.LeadTime.P85Days!.Value)} dias, sobre {flow.LeadTime.Sample} entregas.");
        yield return flow.CycleTime.Unavailable is { } cycleMissing
            ? new($"{id}.cycletime", FactGroups.Flow, $"Cycle time de {label}: sem valor ({cycleMissing})")
            : new($"{id}.cycletime", FactGroups.Flow,
                $"Cycle time de {label}: mediana {Number(flow.CycleTime.MedianDays!.Value)} dias, percentil 85 {Number(flow.CycleTime.P85Days!.Value)} dias, "
                + $"sobre {flow.CycleTime.Sample} entregas com início conhecido ({flow.CycleTime.Excluded} sem início conhecido).");
        yield return flow.Blocked.Unavailable is { } blockedMissing
            ? new($"{id}.blocked", FactGroups.Flow, $"{label} bloqueados: sem valor ({blockedMissing})")
            : new($"{id}.blocked", FactGroups.Flow,
                $"{label} abertos bloqueados: {flow.Blocked.Value} ({flow.Blocked.ViaLink} por vínculo, {flow.Blocked.ViaLabel} por rótulo). Base: {flow.Blocked.Basis}");
        yield return Metric($"{id}.overdue", $"{label} abertos com prazo vencido", flow.Overdue.Value?.ToString(CultureInfo.InvariantCulture), flow.Overdue.Unavailable);
    }

    private static readonly Dictionary<string, string> OutlookText = new()
    {
        [MilestoneOutlook.Done] = "concluído",
        [MilestoneOutlook.Overdue] = "prazo vencido com itens abertos",
        [MilestoneOutlook.NoForecast] = "sem previsão",
        [MilestoneOutlook.NoDueDate] = "sem prazo no provedor",
        [MilestoneOutlook.OnTrack] = "no prazo",
        [MilestoneOutlook.AtRisk] = "em risco",
        [MilestoneOutlook.Behind] = "atrasado em relação ao prazo"
    };

    private static IEnumerable<EvidenceFact> Milestones(ProjectMilestones milestones)
    {
        foreach (var (milestone, index) in milestones.Milestones.Select((milestone, index) => (milestone, index + 1)))
        {
            var label = KindLabel(milestone.Kind);
            var due = milestone.DueDate is { } day ? $"prazo {day:yyyy-MM-dd}" : "sem prazo";
            var scenarios = milestone.Forecast.Scenarios.Count == 0
                ? "sem previsão"
                : string.Join(", ", milestone.Forecast.Scenarios.Select(scenario => $"{scenario.Label.ToLowerInvariant()} até {scenario.By:yyyy-MM-dd}"));
            yield return new($"milestone.{index}", FactGroups.Milestones,
                $"Marco \"{milestone.Name}\" ({label}): {milestone.Delivered} entregues de {milestone.Items} itens, {milestone.Open} abertos, "
                + $"{milestone.ClosedWithoutDelivery} concluídos sem entrega; {due}; previsão: {scenarios}; situação: {OutlookText[milestone.Outlook]} ({milestone.OutlookReason})");
        }

        if (milestones.Omitted > 0)
            yield return new("milestone.omitted", FactGroups.Milestones, $"Há mais {milestones.Omitted} marcos que não foram listados.");
    }

    private static IEnumerable<EvidenceFact> Forecast(DeliveryForecast forecast)
    {
        var id = $"forecast.{forecast.Kind}";
        var label = KindLabel(forecast.Kind);
        if (forecast.Unavailable is { } reason)
        {
            yield return new($"{id}", FactGroups.Forecast, $"Previsão de {label}: indisponível. {reason}");
            yield break;
        }
        yield return new($"{id}", FactGroups.Forecast,
            $"Previsão de {label} para {forecast.Remaining} itens abertos, por simulação sobre {forecast.HistoryWeeks} semanas de histórico: "
            + string.Join("; ", forecast.Scenarios.Select(s => $"{s.Label.ToLowerInvariant()} (percentil {s.Percentile}) em {s.Weeks} semanas, até {s.By:yyyy-MM-dd}"))
            + ".");
        yield return new($"{id}.confidence", FactGroups.Forecast,
            $"Confiança da previsão de {label}: {Confidence(forecast.Confidence!.Value)}."
            + (forecast.ConfidenceReasons.Count > 0 ? " Motivos: " + string.Join(" ", forecast.ConfidenceReasons) : ""));
        foreach (var (factor, index) in forecast.RiskFactors.Select((factor, index) => (factor, index + 1)))
            yield return new($"{id}.factor.{index}", FactGroups.Forecast, $"Fator que pode atrasar {label}: {factor}");
    }

    private static IEnumerable<EvidenceFact> Risks(ProjectRisks risks, bool titlesAllowed)
    {
        if (!risks.HasData) yield break;
        if (risks.Risks.Count == 0)
            yield return new("risk.none", FactGroups.Risks, $"Nenhuma das {risks.RulesEvaluated.Count} regras de risco disparou.");
        foreach (var risk in risks.Risks)
        {
            var items = risk.Items.Count == 0
                ? ""
                : " Itens: " + string.Join(", ", risk.Items.Select(item => titlesAllowed ? $"{item.ExternalId} ({item.Title})" : item.ExternalId)) + ".";
            yield return new($"risk.{risk.Id}", FactGroups.Risks,
                $"Risco detectado por regra: {risk.Title}, severidade {Severity(risk.Severity)}. {string.Join(" ", risk.Evidence)}"
                + (risk.Trend is { } trend ? $" Tendência: {trend}" : "") + items);
        }
    }

    private static EvidenceFact Metric(string id, string label, string? value, string? unavailable) =>
        new(id, FactGroups.Flow, value is null ? $"{label}: sem valor ({unavailable})" : $"{label}: {value}.");

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

    // O dia de quem lê, não o de Greenwich: às 22h de um dia no Brasil, o UTC já está no seguinte.
    private static string Day(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, TimeZoneInfo.Local).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
