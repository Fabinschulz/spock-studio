namespace Mariliz.Application.Integrations;

// Um provedor novo é uma implementação nova registrada em ConnectorRegistry; os serviços não mudam.
public interface IIntegrationConnector
{
    ConnectorDescriptor Descriptor { get; }

    // Recusa com INTEGRATION_SETTINGS_INVALID.
    IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings);

    // Recusa com INTEGRATION_RESOURCE_INVALID.
    string NormalizeResource(string resource);

    Task<ConnectionProbe> ProbeAsync(ConnectorAccess access, CancellationToken ct);

    Task<ResourceProbe> ProbeResourceAsync(ConnectorAccess access, string resource, CancellationToken ct);

    // Da atualização mais antiga para a mais recente; só devolve tipos habilitados no recorte.
    Task<SyncPage> FetchPageAsync(ConnectorAccess access, DataScope scope, SyncCursor cursor, CancellationToken ct);
}
