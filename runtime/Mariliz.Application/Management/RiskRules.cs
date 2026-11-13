using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>
/// As regras de risco. Cada uma diz a condição em <see cref="ProjectRisk.Rule"/>, mostra os números que a dispararam e
/// os itens envolvidos. Nada aqui é probabilidade nem opinião: é contagem sobre o que foi sincronizado.
/// </summary>
public static class RiskRules
{
    public const int MaxItems = 10;
    public const string Origin = "rule";
    public static readonly TimeSpan StalledAfter = TimeSpan.FromDays(14);
    public static readonly TimeSpan ReviewAfter = TimeSpan.FromDays(7);
    public const int CompareWeeks = 4;

    public static readonly IReadOnlyList<string> All =
        ["overdue", "blocked", "stalled-wip", "review-queue", "scope-growth", "throughput-drop"];

    public static List<ProjectRisk> Evaluate(IReadOnlyList<KeyValuePair<string, List<WorkItem>>> byKind, DateTimeOffset now)
    {
        var all = byKind.SelectMany(group => group.Value).ToList();
        var risks = new List<ProjectRisk>();
        Add(risks, Overdue(all, now));
        Add(risks, Blocked(all));
        Add(risks, Stalled(all, now));
        Add(risks, ReviewQueue(all, now));
        foreach (var (kind, items) in byKind)
        {
            Add(risks, ScopeGrowth(kind, items, now));
            Add(risks, ThroughputDrop(kind, items, now));
        }
        return risks.OrderByDescending(risk => risk.Severity).ThenBy(risk => risk.Id, StringComparer.Ordinal).ToList();
    }

    private static ProjectRisk? Overdue(List<WorkItem> items, DateTimeOffset now)
    {
        var overdue = items.Where(item => DeliveryRules.IsOverdue(item, now)).OrderBy(item => item.DueDate).ToList();
        if (overdue.Count == 0) return null;
        var withDueDate = items.Count(item => DeliveryRules.IsOpen(item) && item.DueDate is not null);
        var oldest = (DateOnly.FromDateTime(now.UtcDateTime).DayNumber - overdue[0].DueDate!.Value.DayNumber);
        return new ProjectRisk(
            "overdue",
            "Itens com prazo vencido",
            overdue.Count >= 5 || overdue.Count * 5 >= withDueDate ? RiskSeverity.High : overdue.Count >= 2 ? RiskSeverity.Medium : RiskSeverity.Low,
            "Item aberto com prazo anterior a hoje.",
            [$"{overdue.Count} de {withDueDate} {(withDueDate == 1 ? "item aberto com prazo" : "itens abertos com prazo")} {(overdue.Count == 1 ? "está vencido" : "estão vencidos")}.",
             $"O mais atrasado venceu há {Days(oldest)}."],
            Items(overdue),
            null,
            "Rever com os responsáveis o prazo ou a prioridade dos itens vencidos.",
            Origin);
    }

    private static ProjectRisk? Blocked(List<WorkItem> items)
    {
        var blocked = items.Where(DeliveryRules.IsBlocked).OrderBy(item => item.CreatedAt).ToList();
        if (blocked.Count == 0) return null;
        var byLink = blocked.Count(DeliveryRules.IsBlockedByLink);
        var byLabel = blocked.Count(DeliveryRules.IsBlockedByLabel);
        return new ProjectRisk(
            "blocked",
            "Itens bloqueados",
            blocked.Count >= 5 ? RiskSeverity.High : blocked.Count >= 2 ? RiskSeverity.Medium : RiskSeverity.Low,
            "Item aberto com o rótulo \"blocked\" ou \"bloqueado\", ou com vínculo \"is blocked by\" para um item que ainda não foi concluído.",
            [$"{Count(blocked.Count, "item aberto está bloqueado", "itens abertos estão bloqueados")}: {byLink} por vínculo e {byLabel} por rótulo (um item pode ter os dois)."],
            Items(blocked),
            null,
            "Descobrir o que destrava cada item, ou replanejá-lo.",
            Origin);
    }

    private static ProjectRisk? Stalled(List<WorkItem> items, DateTimeOffset now)
    {
        var stalled = items.Where(item => DeliveryRules.IsInProgress(item) && now - item.UpdatedAt > StalledAfter)
            .OrderBy(item => item.UpdatedAt).ToList();
        if (stalled.Count == 0) return null;
        return new ProjectRisk(
            "stalled-wip",
            "Trabalho em andamento parado",
            stalled.Count >= 5 ? RiskSeverity.High : stalled.Count >= 2 ? RiskSeverity.Medium : RiskSeverity.Low,
            $"Item em andamento sem nenhuma atualização no provedor há mais de {StalledAfter.Days} dias.",
            [$"{Count(stalled.Count, "item em andamento", "itens em andamento")} sem atualização há mais de {StalledAfter.Days} dias.",
             $"O mais antigo está parado há {Days((int)(now - stalled[0].UpdatedAt).TotalDays)}."],
            Items(stalled),
            null,
            "Confirmar se esses itens estão bloqueados, foram abandonados ou precisam de ajuda.",
            Origin);
    }

    private static ProjectRisk? ReviewQueue(List<WorkItem> items, DateTimeOffset now)
    {
        var waiting = items.Where(item => item.Kind == DataKinds.PullRequests && DeliveryRules.IsOpen(item) && now - item.CreatedAt > ReviewAfter)
            .OrderBy(item => item.CreatedAt).ToList();
        if (waiting.Count == 0) return null;
        return new ProjectRisk(
            "review-queue",
            "Pull requests acumulados em revisão",
            waiting.Count >= 5 ? RiskSeverity.High : waiting.Count >= 3 ? RiskSeverity.Medium : RiskSeverity.Low,
            $"Pull request aberto há mais de {ReviewAfter.Days} dias.",
            [$"{Count(waiting.Count, "pull request aberto", "pull requests abertos")} há mais de {ReviewAfter.Days} dias.",
             $"O mais antigo foi aberto há {Days((int)(now - waiting[0].CreatedAt).TotalDays)}."],
            Items(waiting),
            null,
            "Reservar tempo de revisão ou fechar o que não vai seguir.",
            Origin);
    }

    private static ProjectRisk? ScopeGrowth(string kind, List<WorkItem> items, DateTimeOffset now)
    {
        var created = WeeklyHistory.Created(items, now, CompareWeeks).Sum();
        var delivered = WeeklyHistory.Delivered(items, now, CompareWeeks).Sum();
        if (!TrendRules.ScopeGrowing(created, delivered, TrendRules.ScopeRiskRatio)) return null;
        return new ProjectRisk(
            $"scope-growth:{kind}",
            "Escopo crescendo mais rápido do que as entregas",
            created - delivered >= 6 && created >= delivered * 2 ? RiskSeverity.High : RiskSeverity.Medium,
            $"Nas últimas {CompareWeeks} semanas, itens novos superam os entregues em ao menos 50% e em 3 itens.",
            [$"Nas últimas {CompareWeeks} semanas chegaram {created} e foram entregues {delivered}."],
            [],
            $"O trabalho aberto aumentou {created - delivered} itens em {CompareWeeks} semanas.",
            "Rever o que entrou no período e se tudo precisa ser feito agora.",
            Origin);
    }

    private static ProjectRisk? ThroughputDrop(string kind, List<WorkItem> items, DateTimeOffset now)
    {
        var history = WeeklyHistory.Delivered(items, now, CompareWeeks * 2);
        var before = history.Take(CompareWeeks).Sum();
        var after = history.Skip(CompareWeeks).Sum();
        if (!TrendRules.ThroughputDropped(before, after)) return null;
        return new ProjectRisk(
            $"throughput-drop:{kind}",
            "Queda no ritmo de entrega",
            after == 0 ? RiskSeverity.High : RiskSeverity.Medium,
            $"Entregas das últimas {CompareWeeks} semanas são no máximo metade das {CompareWeeks} semanas anteriores.",
            [$"{before} entregas entre {CompareWeeks * 2} e {CompareWeeks} semanas atrás; {after} nas últimas {CompareWeeks} semanas."],
            [],
            $"Caiu de {before} para {after} entregas por período de {CompareWeeks} semanas.",
            "Investigar o que mudou: bloqueios, ausências, itens maiores ou trabalho fora do registro.",
            Origin);
    }

    private static List<RiskItem> Items(IEnumerable<WorkItem> items) =>
        items.Take(MaxItems).Select(item => new RiskItem(item.Kind, item.ExternalId, item.Title)).ToList();

    private static string Count(int value, string one, string many) => $"{value} {(value == 1 ? one : many)}";

    private static string Days(int value) => Count(value, "dia", "dias");

    private static void Add(List<ProjectRisk> risks, ProjectRisk? risk)
    {
        if (risk is not null) risks.Add(risk);
    }
}
