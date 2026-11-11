namespace Mariliz.Application.Integrations;

/// <summary>
/// O contrato de um conector: o que ele precisa para conectar, como confere a credencial e o recurso, e como lê uma página de
/// dados de um recorte. Um provedor novo é uma implementação nova registrada em <see cref="ConnectorRegistry"/>; os serviços não mudam.
/// </summary>
public interface IIntegrationConnector
{
    ConnectorDescriptor Descriptor { get; }

    /// <summary>Valida a configuração não secreta e a devolve na forma canônica. Recusa com <c>INTEGRATION_SETTINGS_INVALID</c>.</summary>
    IReadOnlyDictionary<string, string> NormalizeSettings(IReadOnlyDictionary<string, string> settings);

    /// <summary>Valida o identificador de um recurso e o devolve na forma canônica. Recusa com <c>INTEGRATION_RESOURCE_INVALID</c>.</summary>
    string NormalizeResource(string resource);

    /// <summary>Pergunta ao provedor quem é a credencial e o que ela pode.</summary>
    Task<ConnectionProbe> ProbeAsync(ConnectorAccess access, CancellationToken ct);

    /// <summary>Confere que a credencial alcança o recurso. O nome devolvido é o que o provedor usa.</summary>
    Task<ResourceProbe> ProbeResourceAsync(ConnectorAccess access, string resource, CancellationToken ct);

    /// <summary>Lê uma página do recorte, da atualização mais antiga para a mais recente. Só devolve tipos habilitados no recorte.</summary>
    Task<SyncPage> FetchPageAsync(ConnectorAccess access, DataScope scope, SyncCursor cursor, CancellationToken ct);
}
