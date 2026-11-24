namespace Mariliz.Application.Cloud;

public static class CloudProviders
{
    public const string Aws = "aws";
    public const string Azure = "azure";
    public const string Gcp = "gcp";
}

// Só leitura existe nesta versão; escrita não tem código, nem desligado.
public static class CloudImpacts
{
    public const string Read = "read";
}

/// <param name="Required">Campo vazio é recusado antes de qualquer chamada ao provedor.</param>
public sealed record CloudField(string Name, string Label, string Hint, bool Required);

// Um jeito oficial do provedor de achar credencial. Os campos são nomes (perfil, região, tenant), nunca segredo.
public sealed record CloudMechanism(string Id, string Label, string Description, IReadOnlyList<CloudField> Fields);

public static class CloudColumnKinds
{
    public const string Text = "text";
    public const string Flag = "flag";
}

// Sim e não sem idioma (UI-25): a tela e o MCP escrevem "sim" e "não"; quem decide alguma coisa compara com estes valores,
// nunca com o texto mostrado, que pode mudar de redação ou de idioma.
public static class CloudFlags
{
    public const string True = "true";
    public const string False = "false";

    public static string? Of(bool? value) => value switch { true => True, false => False, null => null };
}

/// <param name="Kind">"flag" quando a célula é um valor de <see cref="CloudFlags"/>; "text" no resto.</param>
public sealed record CloudColumn(string Id, string Label, string Kind = CloudColumnKinds.Text);

public enum CloudRegionUse
{
    // Global: a região não muda o resultado.
    None,

    // Lê uma região por vez; sem região no pedido, vale a padrão da conexão (AWS).
    Required,

    // Filtro opcional: sem região no pedido, vêm todas (Azure, Google).
    Filter
}

/// <param name="Permissions">O que a identidade precisa ter; o que ela tem só a verificação diz.</param>
public sealed record CloudCapability(
    string Id,
    string Title,
    string Description,
    string Service,
    IReadOnlyList<string> Permissions,
    CloudRegionUse Region,
    IReadOnlyList<CloudColumn> Columns,
    string Impact = CloudImpacts.Read);

public sealed record CloudProviderDescriptor(
    string Provider,
    string Name,
    string Version,
    IReadOnlyList<CloudMechanism> Mechanisms,
    IReadOnlyList<CloudCapability> Capabilities,
    string IdentityNote)
{
    public CloudCapability? Capability(string id) => Capabilities.FirstOrDefault(c => c.Id == id);

    public CloudMechanism? Mechanism(string id) => Mechanisms.FirstOrDefault(m => m.Id == id);
}

/// <param name="Values">Só as chaves do mecanismo, já normalizadas pelo provedor.</param>
public sealed record CloudSettings(string Mechanism, IReadOnlyDictionary<string, string> Values)
{
    public string? Value(string name) => Values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    // Todo mecanismo dos três provedores tem região, obrigatória na AWS e opcional nos outros.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Region => Value(CloudFields.Region);
}

public static class CloudFields
{
    public const string Region = "region";
}

/// <param name="Account">Conta da AWS, subscription do Azure ou projeto do Google.</param>
/// <param name="Principal">Quem o provedor diz que é: ARN, usuário ou aplicação do Entra, e-mail da conta.</param>
/// <param name="Tenant">Só no Azure.</param>
public sealed record CloudIdentity(string Account, string? AccountName, string Principal, string? Tenant);

public sealed record CloudQuery(string Capability, string? Region, int Limit);

/// <param name="Service">Código do serviço na lista de preços (AmazonECS, AmazonRDS...).</param>
/// <param name="Attributes">Atributos do produto que precisam bater exatamente.</param>
/// <param name="UsageType">O tipo de uso sem o prefixo da região (Fargate-GB-Hours).</param>
/// <param name="Unit">A unidade do preço (Hrs, GB-Mo).</param>
public sealed record CloudPriceQuery(string Service, string Region, IReadOnlyDictionary<string, string> Attributes, string UsageType, string Unit);

/// <param name="Description">A descrição do preço como o provedor publica.</param>
public sealed record CloudPriceQuote(CloudPriceQuery Query, decimal? UsdPerUnit, string? Description);

/// <param name="Truncated">Havia mais do que o limite pedido.</param>
public sealed record CloudTable(IReadOnlyList<IReadOnlyList<string?>> Rows, bool Truncated);

public enum CloudConnectionStatus
{
    Connected,

    // Aguardando autenticação: o SDK não achou credencial onde a procura.
    NotAuthenticated,

    Expired,

    InvalidIdentity,

    PermissionDenied,

    InvalidRegion,

    Unavailable
}

public enum CloudPermissionState
{
    Unchecked,
    Allowed,
    Denied,
    Error
}

// Nunca carrega texto do provedor.
public sealed record CloudError(string Code, string Message, DateTimeOffset At);

public sealed record CloudPermission(string Capability, CloudPermissionState State, string? Code, DateTimeOffset CheckedAt);

public sealed record CloudConnection(
    string Id,
    string Provider,
    string Name,
    CloudSettings Settings,
    CloudConnectionStatus Status,
    CloudIdentity? Identity,
    IReadOnlyList<CloudPermission> Permissions,
    DateTimeOffset CreatedAt,
    DateTimeOffset CheckedAt,
    CloudError? LastError,
    string ProviderVersion);

// Nasce fechado: sem capacidade e sem agentes. Conexão ativa não libera nada para projeto nenhum.
public sealed record CloudProjectLink(
    string Id,
    string ProjectId,
    string ConnectionId,
    string Provider,
    IReadOnlyList<string> Capabilities,
    bool AgentAccess,
    DateTimeOffset LinkedAt);

public enum CloudCallerKind
{
    Person,
    Agent
}

/// <param name="Client">O nome que o cliente MCP deu de si; não é identidade conferida.</param>
public sealed record CloudCaller(CloudCallerKind Kind, string? Client)
{
    public static readonly CloudCaller Person = new(CloudCallerKind.Person, null);

    public static CloudCaller Agent(string? client) => new(CloudCallerKind.Agent, client);
}

/// <param name="ProjectId">Nulo quando a pessoa explora a própria conexão na interface.</param>
public sealed record CloudReadRequest(string ConnectionId, string Capability, string? Region, int? Limit, string? ProjectId);

public sealed record CloudReadResult(
    string ConnectionId,
    string ConnectionName,
    string Provider,
    string? Account,
    string Capability,
    string? Region,
    IReadOnlyList<CloudColumn> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    bool Truncated,
    DateTimeOffset ReadAt);
