namespace Mariliz.Application.Integrations;

/// <summary>A associação de uma conexão a um projeto, com o recorte de dados e a política de uso como contexto.</summary>
public sealed record ProjectIntegration(
    string Id,
    string ProjectId,
    string ConnectionId,
    string Provider,
    DataScope Scope,
    ContextualizationPolicy Policy,
    DateTimeOffset LinkedAt);
