using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// Checagens determinísticas do desenho, as mesmas para o que a IA propôs e para o que a pessoa montou. Só um desenho
// com todas passando vira arquivo. Uma parte vale para toda nuvem; a outra segue o que cada provedor aceita.
public static partial class IacBlueprintChecks
{
    public const int MaxQueues = 8;

    // O nome do load balancer tem até 32 caracteres: "<projeto>-<env>-alb" com ambiente de até 7.
    public const int MaxProjectLength = 18;

    // Propriedade, não campo: IacProviders aponta para as checagens daqui, e um campo estático dependeria da ordem de inicialização.
    public static IReadOnlyList<string> Providers => IacProviders.All.Select(provider => provider.Id).ToList();

    // Fatos da conta: vêm da pessoa ou da conexão, nunca de quem propõe o desenho (modelo ou agente da sessão). Uma
    // checagem destas falhando pede o dado à pessoa; as outras pedem outro desenho.
    public static readonly IReadOnlySet<string> FactIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "region", "account", "profile", "vpc", "private-subnets", "public-subnets", "load-balancer-subnets", "certificate",
        "database-subnets", "database-subnet", "database-dns", "database-network", "alarm-email", "backend"
    };

    public static IReadOnlyList<IacBlueprintCheck> Check(IacBlueprint blueprint)
    {
        var checks = new List<IacBlueprintCheck>();
        void Add(string id, string description, bool passed, string? detail = null) =>
            checks.Add(new IacBlueprintCheck(id, description, passed, passed ? null : detail));

        var provider = IacProviders.Find(blueprint.Provider);
        Add("provider", "Nuvem suportada na geração (aws, azure ou gcp)", provider is not null);
        Add("project", $"Nome do projeto em minúsculas, dígitos e hífen, até {MaxProjectLength} caracteres",
            Project().IsMatch(blueprint.Project) && blueprint.Project.Length <= MaxProjectLength, "Ex.: loja-api.");
        provider?.Checks(blueprint, Add);

        if (blueprint.Service is { } service)
        {
            Add("service-name", "Nome do serviço em minúsculas, dígitos e hífen, até 10 caracteres", Host().IsMatch(service.Name));
            Add("service-port", "Porta do contêiner entre 1024 e 65535", service.Port is >= 1024 and <= 65535);
            Add("service-health", "Health check com caminho começando por /", service.HealthPath.StartsWith('/') && !service.HealthPath.Contains(' '));
            Add("service-count", "Entre 1 e 10 tarefas", service.DesiredCount is >= 1 and <= 10);
        }
        if (blueprint.Database is { } database)
            Add("database-version", "Versão principal do banco (ex.: 16)", MajorVersion().IsMatch(database.EngineVersion));

        Add("queues", $"Até {MaxQueues} filas, nomes em minúsculas, dígitos e hífen, sem repetir",
            blueprint.Queues.Count <= MaxQueues && blueprint.Queues.All(Queue().IsMatch) && blueprint.Queues.Distinct(StringComparer.Ordinal).Count() == blueprint.Queues.Count);
        Add("components", "Ao menos um componente: serviço, banco ou fila",
            blueprint.Service is not null || blueprint.Database is not null || blueprint.Queues.Count > 0);
        Add("alarm-email", "E-mail de alarme válido, quando informado", blueprint.AlarmEmail is null || Email().IsMatch(blueprint.AlarmEmail));
        return checks;
    }

    // Filas e alarmes não ficam em rede.
    public static bool NeedsNetwork(IacBlueprint b) =>
        b.Database is not null || b.Service is not null && IacProviders.Find(b.Provider) is { ServiceNeedsNetwork: true };

    internal static void Aws(IacBlueprint blueprint, Action<string, string, bool, string?> add)
    {
        void Add(string id, string description, bool passed, string? detail = null) => add(id, description, passed, detail);
        Add("region", "Região AWS no formato sa-east-1", CloudIdentifiers.AwsRegion().IsMatch(blueprint.Region));
        Add("account", "Conta AWS com 12 dígitos, quando informada", blueprint.Account is null || CloudIdentifiers.AwsAccount().IsMatch(blueprint.Account));
        Add("profile", "Perfil com letras, dígitos, ponto, hífen e sublinhado, quando informado",
            blueprint.Profile is null || CloudIdentifiers.AwsProfile().IsMatch(blueprint.Profile));
        if (NeedsNetwork(blueprint))
        {
            Add("vpc", "VPC existente no formato vpc-0123456789abcdef0", Vpc().IsMatch(blueprint.Network.VpcId));
            Add("private-subnets", "Ao menos uma subnet privada para tarefas e banco, no formato subnet-...",
                blueprint.Network.PrivateSubnets.Count >= 1 && blueprint.Network.PrivateSubnets.All(Subnet().IsMatch));
            Add("public-subnets", "Subnets públicas no formato subnet-...", blueprint.Network.PublicSubnets.All(Subnet().IsMatch));
        }
        if (blueprint.Service is { } service)
        {
            Add("service-size", "CPU e memória numa combinação aceita pelo Fargate", Fargate(service.Cpu, service.Memory),
                "Ex.: 256/512, 512/1024, 1024/2048, 2048/4096.");
            Add("load-balancer-subnets", "Ao menos duas subnets públicas, em zonas diferentes, para o load balancer",
                blueprint.Network.PublicSubnets.Count >= 2, "O load balancer exige duas zonas de disponibilidade.");
            Add("certificate", $"Certificado ACM na região {blueprint.Region} para o HTTPS",
                Certificate().IsMatch(service.CertificateArn) && service.CertificateArn.StartsWith($"arn:aws:acm:{blueprint.Region}:", StringComparison.Ordinal),
                "O load balancer do modelo é só HTTPS: informe o ARN do certificado emitido na mesma região.");
        }
        if (blueprint.Database is { } database)
        {
            var options = IacProviderOptionsCatalog.Aws.Database;
            Add("database-engine", "Banco postgres ou mysql", options.Engines.Contains(database.Engine));
            Add("database-class", "Classe de instância RDS (ex.: db.t4g.micro)", RdsClass().IsMatch(database.InstanceClass));
            Add("database-storage", "Armazenamento entre 20 e 1000 GB", options.Storage.Accepts(database.StorageGb));
            Add("database-subnets", "Ao menos duas subnets privadas para o grupo de subnets do banco", blueprint.Network.PrivateSubnets.Count >= 2,
                "O RDS exige subnets em duas zonas de disponibilidade.");
        }
        if (blueprint.Backend is { } backend)
            Add("backend", "Bucket de state e região válidos", CloudIdentifiers.S3Bucket().IsMatch(backend.Bucket) && CloudIdentifiers.AwsRegion().IsMatch(backend.Region));
    }

    internal static void Azure(IacBlueprint blueprint, Action<string, string, bool, string?> add)
    {
        void Add(string id, string description, bool passed, string? detail = null) => add(id, description, passed, detail);
        Add("region", "Região do Azure no formato brazilsouth", CloudIdentifiers.AzureLocation().IsMatch(blueprint.Region));
        Add("account", "Subscription do Azure (GUID)", blueprint.Account is not null && CloudIdentifiers.Guid().IsMatch(blueprint.Account),
            "O provider azurerm exige a subscription; ela também trava o apply nessa subscription.");
        Add("profile", "Tenant do Azure (GUID), quando informado", blueprint.Profile is null || CloudIdentifiers.Guid().IsMatch(blueprint.Profile));
        if (blueprint.Service is { } service)
        {
            Add("service-size", "CPU e memória aceitas pelo Container Apps (memória o dobro da CPU, até 2 vCPU)", ContainerApps(service.Cpu, service.Memory),
                "Ex.: 256/512, 512/1024, 1024/2048, 2048/4096.");
            Add("certificate", "Sem certificado próprio: o HTTPS usa o domínio gerenciado do Container Apps", service.CertificateArn.Length == 0,
                "Domínio e certificado próprios ficam fora do modelo gerado; deixe o campo vazio.");
        }
        if (blueprint.Database is { } database)
        {
            var options = IacProviderOptionsCatalog.Azure.Database;
            Add("database-engine", "Banco postgres (PostgreSQL Flexible Server, só com login do Entra ID)", options.Engines.Contains(database.Engine),
                "No Azure o modelo gera só PostgreSQL, que dispensa senha com o login do Entra ID.");
            Add("database-class", "SKU do Flexible Server (ex.: B_Standard_B1ms, GP_Standard_D2ds_v5)", AzureSku().IsMatch(database.InstanceClass));
            Add("database-storage", "Armazenamento de 32, 64, 128, 256, 512 ou 1024 GB", options.Storage.Accepts(database.StorageGb));
            Add("database-ha", "Alta disponibilidade fora da camada Burstable (B_)", !database.MultiAz || !database.InstanceClass.StartsWith("B_", StringComparison.Ordinal),
                "O Flexible Server não oferece redundância de zona na camada Burstable.");
            Add("database-subnet", "Subnet delegada ao PostgreSQL (id completo do Azure)",
                blueprint.Network.PrivateSubnets.Count >= 1 && AzureSubnet().IsMatch(blueprint.Network.PrivateSubnets[0]),
                "Ex.: /subscriptions/<id>/resourceGroups/<grupo>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<subnet>.");
            Add("database-dns", "Zona DNS privada *.postgres.database.azure.com (id completo)",
                blueprint.Network.PrivateDnsZone is { } zone && AzurePostgresZone().IsMatch(zone));
        }
        if (blueprint.Backend is { } backend)
            Add("backend", "Storage account, grupo de recursos e container do state válidos",
                CloudIdentifiers.AzureStorage().IsMatch(backend.Bucket) && backend.ResourceGroup is { Length: > 0 } && backend.Container is { } container && CloudIdentifiers.AzureContainer().IsMatch(container));
    }

    internal static void Google(IacBlueprint blueprint, Action<string, string, bool, string?> add)
    {
        void Add(string id, string description, bool passed, string? detail = null) => add(id, description, passed, detail);
        Add("region", "Região do Google Cloud no formato southamerica-east1", GoogleRegion().IsMatch(blueprint.Region));
        Add("account", "Projeto do Google Cloud (6 a 30 caracteres: minúsculas, dígitos e hífen)",
            blueprint.Account is not null && CloudIdentifiers.GoogleProject().IsMatch(blueprint.Account), "O provider recebe o projeto; ele também é o escopo dos recursos.");
        if (blueprint.Service is { } service)
        {
            Add("service-size", "CPU e memória aceitas pelo Cloud Run (1, 2 ou 4 vCPU)", CloudRun(service.Cpu, service.Memory),
                "Ex.: 1024/512, 1024/2048, 2048/4096, 4096/8192.");
            Add("certificate", "Sem certificado próprio: o HTTPS usa o endereço gerenciado do Cloud Run", service.CertificateArn.Length == 0,
                "Domínio e certificado próprios ficam fora do modelo gerado; deixe o campo vazio.");
        }
        if (blueprint.Database is { } database)
        {
            var options = IacProviderOptionsCatalog.Gcp.Database;
            Add("database-engine", "Banco postgres ou mysql", options.Engines.Contains(database.Engine));
            Add("database-class", "Tier do Cloud SQL (ex.: db-f1-micro, db-custom-1-3840)", CloudSqlTier().IsMatch(database.InstanceClass));
            Add("database-storage", "Armazenamento entre 10 e 1000 GB", options.Storage.Accepts(database.StorageGb));
            Add("database-network", "Rede VPC com acesso privado a serviços (projects/<projeto>/global/networks/<rede>)",
                GoogleNetwork().IsMatch(blueprint.Network.VpcId), "O Cloud SQL do modelo tem só IP privado.");
        }
        if (blueprint.Backend is { } backend)
            Add("backend", "Bucket de state válido", CloudIdentifiers.GcsBucket().IsMatch(backend.Bucket));
    }

    private static bool Fargate(int cpu, int memory) => cpu switch
    {
        256 => memory is 512 or 1024 or 2048,
        512 => memory is >= 1024 and <= 4096 && memory % 1024 == 0,
        1024 => memory is >= 2048 and <= 8192 && memory % 1024 == 0,
        2048 => memory is >= 4096 and <= 16384 && memory % 1024 == 0,
        4096 => memory is >= 8192 and <= 30720 && memory % 1024 == 0,
        _ => false
    };

    // Consumo do Container Apps: de 0,25 a 2 vCPU em passos de 0,25, com memória de 2 GiB por vCPU.
    private static bool ContainerApps(int cpu, int memory) => cpu is >= 256 and <= 2048 && cpu % 256 == 0 && memory == cpu * 2;

    private static bool CloudRun(int cpu, int memory) => cpu switch
    {
        1024 => memory is >= 512 and <= 4096,
        2048 => memory is >= 1024 and <= 8192,
        4096 => memory is >= 2048 and <= 16384,
        _ => false
    } && memory % 256 == 0;

    [GeneratedRegex("^[a-z][a-z0-9-]*[a-z0-9]$")]
    private static partial Regex Project();

    [GeneratedRegex("^vpc-[0-9a-f]{8,17}$")]
    private static partial Regex Vpc();

    [GeneratedRegex("^subnet-[0-9a-f]{8,17}$")]
    private static partial Regex Subnet();

    [GeneratedRegex("^[a-z][a-z0-9-]{0,9}$")]
    private static partial Regex Host();

    [GeneratedRegex("^arn:aws:acm:[a-z0-9-]+:[0-9]{12}:certificate/[0-9a-f-]{36}$")]
    private static partial Regex Certificate();

    [GeneratedRegex(@"^[0-9]{1,2}(\.[0-9])?$")]
    private static partial Regex MajorVersion();

    [GeneratedRegex(@"^db\.[a-z0-9]+\.[a-z0-9]+$")]
    private static partial Regex RdsClass();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,38}[a-z0-9]$")]
    private static partial Regex Queue();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Email();

    [GeneratedRegex("^(B|GP|MO)_Standard_[A-Za-z0-9_]+$")]
    private static partial Regex AzureSku();

    [GeneratedRegex(@"^/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/]+/providers/Microsoft\.Network/virtualNetworks/[^/]+/subnets/[^/]+$", RegexOptions.IgnoreCase)]
    private static partial Regex AzureSubnet();

    [GeneratedRegex(@"^/subscriptions/[0-9a-fA-F-]{36}/resourceGroups/[^/]+/providers/Microsoft\.Network/privateDnsZones/[a-z0-9.-]+\.postgres\.database\.azure\.com$", RegexOptions.IgnoreCase)]
    private static partial Regex AzurePostgresZone();

    [GeneratedRegex("^[a-z]+-[a-z]+[0-9]{1,2}$")]
    private static partial Regex GoogleRegion();

    [GeneratedRegex("^db-(f1-micro|g1-small|custom-[0-9]{1,2}-[0-9]{3,6}|perf-optimized-N-[0-9]{1,2})$")]
    private static partial Regex CloudSqlTier();

    [GeneratedRegex("^projects/[a-z][a-z0-9-]{4,28}[a-z0-9]/global/networks/[a-z0-9-]{1,63}$")]
    private static partial Regex GoogleNetwork();
}
