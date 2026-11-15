using System.Globalization;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;

namespace Mariliz.Application.Mcp;

internal sealed record ManagementSnapshot(ProjectMetrics Metrics, ProjectRisks Risks, ProjectMilestones Milestones);

// Só números e textos do motor. Título de issue, nome de marco e corpo de item vêm do serviço externo e ficam de fora: a
// política de contexto de cada integração nasce fechada para agentes, e a IDE é um agente.
internal static class ManagementBriefing
{
    public const int Risks = 5;
    private static readonly CultureInfo Number = CultureInfo.GetCultureInfo("pt-BR");

    public static IEnumerable<string> Sections(ManagementSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            yield return "## Integrações\nNão foi possível ler as integrações agora.";
            yield break;
        }
        yield return Integrations(snapshot.Metrics);
        if (snapshot.Metrics.Sources.Count > 0) yield return Management(snapshot);
    }

    private static string Integrations(ProjectMetrics metrics)
    {
        if (metrics.Sources.Count == 0) return "## Integrações\nNenhuma integração vinculada no Spock.";
        return string.Join("\n", metrics.Sources
            .Select(source => $"- {Provider(source.Provider)} · {source.ResourceName} · {LastSync(source)}")
            .Prepend("## Integrações"));
    }

    private static string Management(ManagementSnapshot snapshot)
    {
        var metrics = snapshot.Metrics;
        var lines = new List<string> { $"## Gestão (últimas {metrics.PeriodWeeks} semanas, calculado pelo Spock sobre o que foi sincronizado)" };
        if (!metrics.Complete) lines.Add("A última sincronização de alguma fonte não terminou: os números valem para o que chegou.");
        lines.AddRange(metrics.Kinds.Select(Flow));

        var risks = snapshot.Risks.Risks;
        lines.Add(risks.Count == 0 ? "Riscos: nenhum pelas regras avaliadas." : $"Riscos ({risks.Count}):");
        lines.AddRange(risks.OrderByDescending(risk => risk.Severity).Take(Risks)
            .Select(risk => $"- {risk.Title} ({Severity(risk.Severity)}{(risk.Items.Count > 0 ? ", " + Plural(risk.Items.Count, "item", "itens") : "")}). {risk.Recommendation}"));
        if (risks.Count > Risks) lines.Add($"- e mais {risks.Count - Risks}.");

        lines.Add(Milestones(snapshot.Milestones));
        return string.Join("\n", lines);
    }

    private static string Flow(KindFlow flow)
    {
        var parts = new List<string> { $"{flow.Items} no total, {flow.Open} em aberto" };
        if (flow.Throughput.Total is { } delivered)
            parts.Add(flow.Throughput.WeeklyAverage is { } average
                ? $"{Plural(delivered, "entregue", "entregues")} no período ({Format(average)} por semana)"
                : $"{Plural(delivered, "entregue", "entregues")} no período");
        if (flow.LeadTime.MedianDays is { } lead) parts.Add($"lead time mediano {Format(lead)} dias");
        if (flow.Blocked.Value is > 0 and var blocked) parts.Add(Plural(blocked, "bloqueado", "bloqueados"));
        if (flow.Overdue.Value is > 0 and var overdue) parts.Add(Plural(overdue, "atrasado", "atrasados"));
        return $"- {Kind(flow.Kind)}: {string.Join("; ", parts)}.";
    }

    private static string Milestones(ProjectMilestones milestones)
    {
        var open = milestones.Milestones.Where(milestone => milestone.Open > 0).ToList();
        if (open.Count == 0) return "Marcos: nenhum em aberto.";
        var late = open.Count(milestone => milestone.Outlook is MilestoneOutlook.Overdue or MilestoneOutlook.Behind);
        var atRisk = open.Count(milestone => milestone.Outlook == MilestoneOutlook.AtRisk);
        var next = open.Where(milestone => milestone.DueDate is not null).MinBy(milestone => milestone.DueDate);
        var text = $"Marcos: {open.Count} em aberto, {late} atrasados ou fora do ritmo, {atRisk} em risco";
        return next is null ? text + "." : $"{text}; o próximo prazo é {next.DueDate:yyyy-MM-dd}, {Outlook(next.Outlook)}.";
    }

    private static string LastSync(MetricSource source) => source.LastSyncedAt is { } at
        ? $"última sincronização {at:yyyy-MM-dd HH:mm} UTC, {Status(source.LastSyncStatus)}"
        : "ainda não sincronizada";

    private static string Provider(string provider) => provider switch
    {
        IntegrationProviders.GitHub => "GitHub",
        IntegrationProviders.Jira => "Jira",
        _ => provider
    };

    private static string Kind(string kind) => kind switch
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
        _ => "sem resultado"
    };

    private static string Severity(RiskSeverity severity) => severity switch
    {
        RiskSeverity.High => "alta",
        RiskSeverity.Medium => "média",
        _ => "baixa"
    };

    private static string Outlook(string outlook) => outlook switch
    {
        MilestoneOutlook.OnTrack => "no ritmo",
        MilestoneOutlook.AtRisk => "em risco",
        MilestoneOutlook.Behind => "fora do ritmo",
        MilestoneOutlook.Overdue => "vencido",
        MilestoneOutlook.NoForecast => "sem previsão",
        _ => "sem previsão"
    };

    private static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    private static string Format(double value) => value.ToString("0.#", Number);
}
