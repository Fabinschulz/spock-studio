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

// Para o provedor com lista pública de preços consultável pela conexão (a AWS, pela Price List Query API). Os preços são os
// públicos sob demanda, iguais para qualquer conta: a conexão só autentica a consulta.
public interface ICloudPriceSource
{
    // Uma cotação por consulta, na mesma ordem; UsdPerUnit null quando a lista não tem um preço único para ela.
    Task<IReadOnlyList<CloudPriceQuote>> PricesAsync(CloudSettings settings, IReadOnlyList<CloudPriceQuery> queries, CancellationToken ct);
}

// Para o provedor em que os nomes da conexão não levam a ferramenta de IaC à mesma identidade que as leituras usam: a AWS
// com AssumeRole, que a ferramenta não sabe montar a partir de um Role ARN guardado só no Spock (P0-1). As variáveis valem
// para um processo e não são guardadas.
public interface ICloudProcessCredentials
{
    // Vazio quando os nomes da conexão bastam. Recusa com os códigos de CloudErrors, como uma leitura.
    Task<IReadOnlyDictionary<string, string>> ProcessEnvironmentAsync(CloudSettings settings, CancellationToken ct);
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
