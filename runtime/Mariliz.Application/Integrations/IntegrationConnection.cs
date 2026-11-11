namespace Mariliz.Application.Integrations;

/// <summary>
/// A capacidade técnica de falar com um sistema externo: quem autenticou e com que permissões. Não dá acesso a dado nenhum
/// por si só; o que um projeto pode ler é decidido em <see cref="ProjectIntegration"/>. A credencial fica no cofre, fora deste registro.
/// </summary>
/// <param name="Scopes">O que o provedor informou sobre a credencial. Vazio quando ele não informa (o acesso é o da conta).</param>
/// <param name="Settings">Configuração não secreta do conector (ex.: o endereço do site do Jira).</param>
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
