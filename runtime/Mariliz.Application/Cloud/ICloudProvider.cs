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

/// <param name="Variables">As variáveis com que a ferramenta de IaC acha a identidade da conexão. Só nomes (perfil, região,
/// subscription, projeto, caminho do arquivo da conta de serviço), nunca segredo.</param>
/// <param name="Scope">O perfil (AWS), a subscription (Azure) ou o projeto (Google): o valor padrão do bootstrap de state.</param>
public sealed record CloudProcessNames(IReadOnlyDictionary<string, string> Variables, string? Scope);

public static class CloudProcessVariables
{
    // Campo vazio não vira variável vazia: a ferramenta trataria "" como valor e não procuraria o padrão dela.
    public static IReadOnlyDictionary<string, string> Of(params (string Name, string? Value)[] variables) =>
        variables.Where(variable => !string.IsNullOrWhiteSpace(variable.Value))
            .ToDictionary(variable => variable.Name, variable => variable.Value!, StringComparer.Ordinal);
}

// Como a ferramenta de IaC roda com a identidade da conexão. Só o conector sabe o nome dos próprios campos: se a IaC lesse
// a chave crua, renomear um campo ainda compilaria e a ferramenta cairia na cadeia padrão, que pode ser outra conta.
public interface ICloudProcessCredentials
{
    CloudProcessNames ProcessNames(CloudSettings settings);

    // Para quando os nomes não levam à mesma identidade que as leituras usam: a AWS com AssumeRole, que a ferramenta não
    // sabe montar a partir de um Role ARN guardado só no Spock (P0-1). As variáveis valem para um processo e não são
    // guardadas. Vazio quando os nomes bastam. Recusa com os códigos de CloudErrors, como uma leitura.
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
