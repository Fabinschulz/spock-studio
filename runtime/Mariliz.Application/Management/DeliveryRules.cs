using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

/// <summary>
/// O que conta como entregue, aberto e em andamento. É o único lugar com essa regra: portfólio e indicadores leem daqui,
/// e a documentação dos indicadores descreve exatamente isto.
/// </summary>
public static class DeliveryRules
{
    // Status de fechamento do GitHub que não são entrega: issue descartada ou repetida.
    private static readonly HashSet<string> ClosedWithoutDelivery = new(StringComparer.Ordinal) { "not_planned", "duplicate" };

    /// <summary>
    /// Concluído, com data de conclusão, e não descartado. Pull request fechado sem merge não é entrega. No Jira, item
    /// concluído sem data de resolução fica de fora, porque não há quando contá-lo.
    /// </summary>
    public static bool IsDelivered(WorkItem item) =>
        item.StatusCategory == StatusCategories.Done
        && item.ClosedAt is not null
        && !ClosedWithoutDelivery.Contains(item.Status)
        && !(item.Kind == DataKinds.PullRequests && item.Status == "closed");

    public static bool IsOpen(WorkItem item) => item.StatusCategory != StatusCategories.Done;

    public static bool IsInProgress(WorkItem item) => item.StatusCategory == StatusCategories.InProgress;

    /// <summary>Aberto com prazo anterior ao dia de hoje (UTC). Sem prazo informado, não está atrasado: não se sabe.</summary>
    public static bool IsOverdue(WorkItem item, DateTimeOffset now) =>
        IsOpen(item) && item.DueDate is { } due && due < DateOnly.FromDateTime(now.UtcDateTime);
}
