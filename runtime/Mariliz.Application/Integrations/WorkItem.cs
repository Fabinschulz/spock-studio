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
/// <param name="BlockedBy">
/// Identificadores dos itens ainda abertos que bloqueiam este, pelos vínculos de bloqueio que o provedor entrega. Lista vazia
/// quer dizer "o vínculo foi lido e não há"; <c>null</c> quer dizer "não foi lido" (o provedor não tem vínculos, ou o item foi
/// sincronizado antes de eles serem lidos).
/// </param>
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
    IReadOnlyList<string>? BlockedBy = null);
