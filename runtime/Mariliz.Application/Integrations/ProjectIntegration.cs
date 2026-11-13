namespace Mariliz.Application.Integrations;

public sealed record ProjectIntegration(
    string Id,
    string ProjectId,
    string ConnectionId,
    string Provider,
    DataScope Scope,
    ContextualizationPolicy Policy,
    DateTimeOffset LinkedAt);
