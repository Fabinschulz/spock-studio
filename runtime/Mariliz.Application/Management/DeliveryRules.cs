using Mariliz.Application.Integrations;

namespace Mariliz.Application.Management;

// Único lugar com esta regra: portfólio, indicadores e a documentação dos indicadores dependem dela.
public static class DeliveryRules
{
    private static readonly HashSet<string> ClosedWithoutDelivery = new(StringComparer.Ordinal) { "not_planned", "duplicate" };

    // No Jira, item concluído sem data de resolução fica de fora porque não há quando contá-lo.
    public static bool IsDelivered(WorkItem item) =>
        item.StatusCategory == StatusCategories.Done
        && item.ClosedAt is not null
        && !ClosedWithoutDelivery.Contains(item.Status)
        && !(item.Kind == DataKinds.PullRequests && item.Status == "closed");

    public static bool IsOpen(WorkItem item) => item.StatusCategory != StatusCategories.Done;

    public static readonly IReadOnlyList<string> BlockedLabels = ["blocked", "bloqueado", "bloqueada"];

    public static bool IsBlockedByLabel(WorkItem item) =>
        IsOpen(item) && item.Labels.Any(label => BlockedLabels.Contains(label, StringComparer.OrdinalIgnoreCase));

    public static bool IsBlockedByLink(WorkItem item) => IsOpen(item) && item.BlockedBy is { Count: > 0 };

    public static bool IsBlocked(WorkItem item) => IsBlockedByLabel(item) || IsBlockedByLink(item);

    public static bool IsInProgress(WorkItem item) => item.StatusCategory == StatusCategories.InProgress;

    // Sem prazo informado não está atrasado: não se sabe.
    public static bool IsOverdue(WorkItem item, DateTimeOffset now) =>
        IsOpen(item) && item.DueDate is { } due && due < DateOnly.FromDateTime(now.UtcDateTime);
}
