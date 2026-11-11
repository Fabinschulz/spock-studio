namespace Mariliz.Application.Integrations;

/// <summary>
/// Uma sincronização de uma integração de projeto. <see cref="Incremental"/> diz se partiu de um ponto de retomada;
/// <see cref="Created"/>, <see cref="Updated"/> e <see cref="Unchanged"/> somam <see cref="Fetched"/>.
/// </summary>
public sealed record SyncExecution(
    string Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    SyncStatus Status,
    bool Incremental,
    int Pages,
    int Fetched,
    int Created,
    int Updated,
    int Unchanged,
    IntegrationError? Error);
