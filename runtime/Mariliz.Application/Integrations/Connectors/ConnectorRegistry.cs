namespace Mariliz.Application.Integrations;

// É a única lista: catálogo e validação de provedor saem daqui.
public sealed class ConnectorRegistry(IEnumerable<IIntegrationConnector> connectors)
{
    private readonly Dictionary<string, IIntegrationConnector> _byProvider =
        connectors.ToDictionary(c => c.Descriptor.Provider, StringComparer.Ordinal);

    public IReadOnlyList<ConnectorDescriptor> Catalog() =>
        _byProvider.Values.Select(c => c.Descriptor).OrderBy(d => d.Name, StringComparer.Ordinal).ToList();

    public IIntegrationConnector Require(string provider) =>
        _byProvider.TryGetValue(provider, out var connector) ? connector : throw IntegrationErrors.ProviderUnknown();
}
