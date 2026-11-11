namespace Mariliz.Application.Integrations;

/// <summary>
/// Uma atividade lida de um provedor (issue, pull request), no formato comum que as visões do produto consomem. Só fatos do
/// provedor: nada aqui é calculado ou inferido.
/// </summary>
/// <param name="Kind">Um dos <see cref="DataKinds"/>.</param>
/// <param name="ExternalId">Identificador no provedor: número no GitHub, chave no Jira.</param>
/// <param name="Status">O status como o provedor escreve.</param>
/// <param name="StatusCategory">Um dos <see cref="StatusCategories"/>.</param>
/// <param name="Transitions">Mudanças de status, quando o provedor as entrega junto com o item.</param>
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
    IReadOnlyList<WorkItemTransition> Transitions);
