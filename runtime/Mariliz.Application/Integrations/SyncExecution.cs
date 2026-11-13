namespace Mariliz.Application.Integrations;

// Created, Updated e Unchanged somam Fetched.
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
