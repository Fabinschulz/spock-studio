using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Azure;

// A credencial é a que o Azure.Identity acha (login do Azure CLI, variáveis de ambiente, identidade gerenciada, Visual
// Studio): o Spock guarda só tenant, subscription e região de filtro. Login interativo fica desligado.
public sealed partial class AzureCloudProvider : ICloudProvider, ICloudProcessCredentials
{
    public const string CliMechanism = "azure-cli";
    public const string DefaultMechanism = "default";
    public const string TenantField = "tenantId";
    public const string SubscriptionField = "subscriptionId";
    private const string ArmScope = "https://management.azure.com/.default";

    private static readonly CloudField[] Fields =
    [
        new(TenantField, "Tenant", "Opcional. GUID ou domínio do tenant do Microsoft Entra ID. Sem ele, vale o da credencial.", Required: false),
        new(SubscriptionField, "Subscription", "Opcional. GUID da subscription. Sem ele, vale a padrão da identidade.", Required: false),
        new(CloudFields.Region, "Região (filtro)", "Opcional, por exemplo brazilsouth. Só filtra as leituras que aceitam região.", Required: false)
    ];

    private static CloudColumn C(string id, string label) => new(id, label);

    // O provider azurerm acha a credencial sozinho (az login, variáveis, identidade gerenciada); só a subscription e o tenant vão.
    public CloudProcessNames ProcessNames(CloudSettings settings) => new(CloudProcessVariables.Of(
        ("ARM_SUBSCRIPTION_ID", settings.Value(SubscriptionField)),
        ("ARM_TENANT_ID", settings.Value(TenantField))), settings.Value(SubscriptionField));

    public Task<IReadOnlyDictionary<string, string>> ProcessEnvironmentAsync(CloudSettings settings, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    private static readonly CloudColumn[] ResourceColumns = [C("name", "Nome"), C("resourceGroup", "Grupo de recursos"), C("location", "Região"), C("sku", "SKU"), C("kind", "Tipo")];

    private static readonly Dictionary<string, string> ResourceTypes = new(StringComparer.Ordinal)
    {
        ["azure.virtual_machines"] = "Microsoft.Compute/virtualMachines",
        ["azure.storage_accounts"] = "Microsoft.Storage/storageAccounts",
        ["azure.web_apps"] = "Microsoft.Web/sites",
        ["azure.aks_clusters"] = "Microsoft.ContainerService/managedClusters",
        ["azure.sql_servers"] = "Microsoft.Sql/servers"
    };

    public CloudProviderDescriptor Descriptor { get; } = new(
        CloudProviders.Azure,
        "Microsoft Azure",
        "1.0.0",
        [
            new CloudMechanism(CliMechanism, "Login do Azure CLI", "A conta do az login desta máquina (AzureCliCredential).", Fields),
            new CloudMechanism(DefaultMechanism, "Cadeia padrão do Azure.Identity",
                "DefaultAzureCredential: variáveis de ambiente (service principal), workload identity, identidade gerenciada, Visual Studio, Azure CLI, Azure PowerShell e Azure Developer CLI, nessa ordem. Sem navegador.",
                Fields)
        ],
        [
            new("azure.subscriptions", "Subscriptions", "Subscriptions que a identidade enxerga.", "Resource Manager",
                ["Microsoft.Resources/subscriptions/read"], CloudRegionUse.None, [C("id", "Id"), C("name", "Nome"), C("state", "Estado")]),
            new("azure.resource_groups", "Grupos de recursos", "Grupos de recursos da subscription.", "Resource Manager",
                ["Microsoft.Resources/subscriptions/resourceGroups/read"], CloudRegionUse.Filter, [C("name", "Nome"), C("location", "Região"), C("state", "Provisionamento")]),
            new("azure.virtual_machines", "Máquinas virtuais", "VMs da subscription.", "Compute", ["Microsoft.Compute/virtualMachines/read"], CloudRegionUse.Filter, ResourceColumns),
            new("azure.storage_accounts", "Contas de armazenamento", "Contas de Storage da subscription.", "Storage", ["Microsoft.Storage/storageAccounts/read"], CloudRegionUse.Filter, ResourceColumns),
            new("azure.web_apps", "App Service", "Web apps e function apps da subscription.", "App Service", ["Microsoft.Web/sites/read"], CloudRegionUse.Filter, ResourceColumns),
            new("azure.aks_clusters", "Clusters AKS", "Clusters Kubernetes gerenciados da subscription.", "AKS", ["Microsoft.ContainerService/managedClusters/read"], CloudRegionUse.Filter, ResourceColumns),
            new("azure.sql_servers", "Servidores SQL", "Servidores do Azure SQL da subscription.", "SQL", ["Microsoft.Sql/servers/read"], CloudRegionUse.Filter, ResourceColumns)
        ],
        "Tenant e quem é vêm do token do Entra ID; a subscription, do Resource Manager.");

    public CloudSettings Normalize(CloudSettings settings)
    {
        var values = new Dictionary<string, string>(settings.Values, StringComparer.Ordinal);
        if (values.TryGetValue(TenantField, out var tenant) && !GuidPattern().IsMatch(tenant) && !DomainPattern().IsMatch(tenant))
            throw CloudErrors.SettingsInvalid("O tenant é um GUID ou um domínio como contoso.onmicrosoft.com.");
        if (values.TryGetValue(SubscriptionField, out var subscription))
            values[SubscriptionField] = GuidPattern().IsMatch(subscription) ? subscription.ToLowerInvariant()
                : throw CloudErrors.SettingsInvalid("A subscription é um GUID, como aparece no portal.");
        if (values.TryGetValue(CloudFields.Region, out var region)) values[CloudFields.Region] = NormalizeRegion(region);
        return settings with { Values = values };
    }

    public string NormalizeRegion(string region)
    {
        var clean = region.Trim().ToLowerInvariant();
        return LocationPattern().IsMatch(clean) ? clean : throw CloudErrors.RegionInvalid($"'{region.Trim()}' não tem o formato de uma região do Azure, como brazilsouth.");
    }

    public async Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct)
    {
        try
        {
            var credential = Credential(settings);
            var token = await credential.GetTokenAsync(new TokenRequestContext([ArmScope]), ct);
            var claims = Claims(token.Token);
            var subscription = await SubscriptionAsync(Arm(credential, settings), settings, ct);
            var principal = claims.Upn ?? claims.AppId ?? claims.ObjectId ?? "identidade sem nome no token";
            return new CloudIdentity(subscription.Data.SubscriptionId ?? "", subscription.Data.DisplayName, principal, claims.TenantId);
        }
        catch (Exception ex) when (AzureErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    public async Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct)
    {
        try
        {
            var arm = Arm(Credential(settings), settings);
            return query.Capability switch
            {
                "azure.subscriptions" => await SubscriptionsAsync(arm, query.Limit, ct),
                "azure.resource_groups" => await GroupsAsync(await SubscriptionAsync(arm, settings, ct), query, ct),
                var id when ResourceTypes.TryGetValue(id, out var type) => await ResourcesAsync(await SubscriptionAsync(arm, settings, ct), type, query, ct),
                _ => throw CloudErrors.CapabilityUnknown()
            };
        }
        catch (Exception ex) when (AzureErrors.Map(ex, settings, ct) is { } mapped)
        {
            throw mapped;
        }
    }

    private static TokenCredential Credential(CloudSettings settings)
    {
        var tenant = settings.Value(TenantField);
        return settings.Mechanism switch
        {
            CliMechanism => new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenant, ProcessTimeout = TimeSpan.FromSeconds(20) }),
            DefaultMechanism => new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = tenant, ExcludeInteractiveBrowserCredential = true }),
            _ => throw CloudErrors.MechanismUnknown()
        };
    }

    private static ArmClient Arm(TokenCredential credential, CloudSettings settings)
    {
        var options = new ArmClientOptions();
        options.Retry.MaxRetries = 2;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
        return new ArmClient(credential, settings.Value(SubscriptionField), options);
    }

    private static async Task<SubscriptionResource> SubscriptionAsync(ArmClient arm, CloudSettings settings, CancellationToken ct) =>
        settings.Value(SubscriptionField) is { } id
            ? (await arm.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(id)).GetAsync(ct)).Value
            : await arm.GetDefaultSubscriptionAsync(ct);

    private static async Task<CloudTable> SubscriptionsAsync(ArmClient arm, int limit, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<string?>>();
        await foreach (var subscription in arm.GetSubscriptions().GetAllAsync(ct))
        {
            rows.Add([subscription.Data.SubscriptionId, subscription.Data.DisplayName, subscription.Data.State?.ToString()]);
            if (rows.Count > limit) break;
        }
        return Table(rows, limit);
    }

    private static async Task<CloudTable> GroupsAsync(SubscriptionResource subscription, CloudQuery query, CancellationToken ct)
    {
        var rows = new List<IReadOnlyList<string?>>();
        await foreach (var group in subscription.GetResourceGroups().GetAllAsync(cancellationToken: ct))
        {
            var location = group.Data.Location.Name;
            if (query.Region is not null && !string.Equals(location, query.Region, StringComparison.OrdinalIgnoreCase)) continue;
            rows.Add([group.Data.Name, location, group.Data.ResourceGroupProvisioningState]);
            if (rows.Count > query.Limit) break;
        }
        return Table(rows, query.Limit);
    }

    // Recursos genéricos por tipo: um pacote só (Azure.ResourceManager) serve a todos os serviços desta lista.
    private static async Task<CloudTable> ResourcesAsync(SubscriptionResource subscription, string type, CloudQuery query, CancellationToken ct)
    {
        var filter = $"resourceType eq '{type}'" + (query.Region is null ? "" : $" and location eq '{query.Region}'");
        var rows = new List<IReadOnlyList<string?>>();
        await foreach (var resource in subscription.GetGenericResourcesAsync(filter, top: query.Limit + 1, cancellationToken: ct))
        {
            rows.Add([resource.Data.Name, resource.Data.Id.ResourceGroupName, resource.Data.Location.Name, resource.Data.Sku?.Name, resource.Data.Kind]);
            if (rows.Count > query.Limit) break;
        }
        return Table(rows, query.Limit);
    }

    private static CloudTable Table(List<IReadOnlyList<string?>> rows, int limit) => new(rows.Take(limit).ToList(), rows.Count > limit);

    private sealed record TokenClaims(string? TenantId, string? ObjectId, string? Upn, string? AppId);

    // Só leitura do payload para saber quem é: a assinatura já foi conferida por quem emitiu e quem consome (o ARM).
    private static TokenClaims Claims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return new TokenClaims(null, null, null, null);
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        string? Get(string name) => json.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new TokenClaims(Get("tid"), Get("oid"), Get("upn") ?? Get("unique_name") ?? Get("preferred_username"), Get("appid") ?? Get("azp"));
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9\-]{0,62}(\.[A-Za-z0-9][A-Za-z0-9\-]{0,62})+$")]
    private static partial Regex DomainPattern();

    [GeneratedRegex(@"^[a-z][a-z0-9]{2,39}$")]
    private static partial Regex LocationPattern();
}
