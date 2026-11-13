namespace Mariliz.Application.Integrations;

// Não dá acesso a dado nenhum por si só: o que um projeto pode ler é decidido em ProjectIntegration. A credencial fica no cofre, fora deste registro.
/// <param name="Scopes">Vazio quando o provedor não informa (o acesso é o da conta).</param>
public sealed record IntegrationConnection(
    string Id,
    string Provider,
    string Name,
    string Account,
    ConnectionStatus Status,
    IReadOnlyList<string> Scopes,
    IReadOnlyDictionary<string, string> Settings,
    DateTimeOffset ConnectedAt,
    DateTimeOffset CheckedAt,
    DateTimeOffset? LastSyncAt,
    IntegrationError? LastError,
    string ConnectorVersion);
