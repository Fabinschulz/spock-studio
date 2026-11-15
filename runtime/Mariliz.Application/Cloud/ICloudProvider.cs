namespace Mariliz.Application.Cloud;

// Um provedor novo é uma implementação nova registrada em LocalCloudHub; os serviços não mudam. O SDK fica só na implementação.
// Falhas saem como EngineException com os códigos de CloudErrors, sem texto vindo do provedor.
public interface ICloudProvider
{
    CloudProviderDescriptor Descriptor { get; }

    // Recusa com CLOUD_SETTINGS_INVALID ou CLOUD_REGION_INVALID; devolve só as chaves do mecanismo.
    CloudSettings Normalize(CloudSettings settings);

    // Recusa com CLOUD_REGION_INVALID.
    string NormalizeRegion(string region);

    Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct);

    // As linhas seguem as colunas da capacidade, na mesma ordem.
    Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct);
}

// Para o provedor que responde oficialmente "quais destas permissões eu tenho" (o Google, com testIamPermissions).
// Sem ele, a verificação faz a leitura mínima de cada capacidade.
public interface ICloudPermissionTester
{
    Task<IReadOnlyDictionary<string, bool>> TestPermissionsAsync(CloudSettings settings, IReadOnlyList<string> permissions, CancellationToken ct);
}

// Nunca dentro de um projeto.
public interface ICloudStore
{
    IReadOnlyList<CloudConnection> Connections();

    void SaveConnection(CloudConnection connection);

    void RemoveConnection(string connectionId);

    IReadOnlyList<CloudProjectLink> Links();

    void SaveLink(CloudProjectLink link);

    void RemoveLink(string linkId);
}
