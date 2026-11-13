namespace Mariliz.Application.Integrations;

// Só fatos do provedor: nada aqui é calculado ou inferido.
/// <param name="Milestones">Lista vazia quer dizer "sem marco"; <c>null</c>, que o item foi sincronizado antes de os marcos serem lidos.</param>
/// <param name="BlockedBy">Lista vazia quer dizer "o vínculo foi lido e não há"; <c>null</c> quer dizer "não foi lido" (o provedor não tem vínculos, ou o item foi sincronizado antes de eles serem lidos).</param>
public sealed record WorkItem(
    string Kind,
    string ExternalId,
    string Title,
    string Type,
    string Status,
    string StatusCategory,
    IReadOnlyList<string> Assignees,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    DateOnly? DueDate,
    string? Parent,
    string Url,
    IReadOnlyList<WorkItemTransition> Transitions,
    IReadOnlyList<string>? BlockedBy = null,
    IReadOnlyList<WorkItemMilestone>? Milestones = null);
