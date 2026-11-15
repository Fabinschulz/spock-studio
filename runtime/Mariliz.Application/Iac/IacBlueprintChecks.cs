using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// Checagens determinísticas do desenho, as mesmas para o que a IA propôs e para o que a pessoa montou. Só um desenho
// com todas passando vira arquivo.
public static partial class IacBlueprintChecks
{
    public const int MaxQueues = 8;

    // O nome do load balancer tem até 32 caracteres: "<projeto>-<env>-alb" com ambiente de até 7.
    public const int MaxProjectLength = 18;

    public static IReadOnlyList<IacBlueprintCheck> Check(IacBlueprint blueprint)
    {
        var checks = new List<IacBlueprintCheck>();
        void Add(string id, string description, bool passed, string? detail = null) =>
            checks.Add(new IacBlueprintCheck(id, description, passed, passed ? null : detail));

        Add("provider", "Nuvem suportada na geração (aws)", blueprint.Provider == "aws", "Nesta entrega a geração é só para AWS.");
        Add("project", $"Nome do projeto em minúsculas, dígitos e hífen, até {MaxProjectLength} caracteres",
            Project().IsMatch(blueprint.Project) && blueprint.Project.Length <= MaxProjectLength, "Ex.: loja-api.");
        Add("region", "Região AWS no formato sa-east-1", Region().IsMatch(blueprint.Region));
        Add("account", "Conta AWS com 12 dígitos, quando informada", blueprint.Account is null || Account().IsMatch(blueprint.Account));
        Add("profile", "Perfil com letras, dígitos, ponto, hífen e sublinhado, quando informado",
            blueprint.Profile is null || Profile().IsMatch(blueprint.Profile));
        if (IacAwsGenerator.NeedsNetwork(blueprint))
        {
            Add("vpc", "VPC existente no formato vpc-0123456789abcdef0", Vpc().IsMatch(blueprint.Network.VpcId));
            Add("private-subnets", "Ao menos uma subnet privada para tarefas e banco, no formato subnet-...",
                blueprint.Network.PrivateSubnets.Count >= 1 && blueprint.Network.PrivateSubnets.All(Subnet().IsMatch));
            Add("public-subnets", "Subnets públicas no formato subnet-...", blueprint.Network.PublicSubnets.All(Subnet().IsMatch));
        }

        if (blueprint.Service is { } service)
        {
            Add("service-name", "Nome do serviço em minúsculas, dígitos e hífen, até 10 caracteres", Host().IsMatch(service.Name));
            Add("service-port", "Porta do contêiner entre 1024 e 65535", service.Port is >= 1024 and <= 65535);
            Add("service-health", "Health check com caminho começando por /", service.HealthPath.StartsWith('/') && !service.HealthPath.Contains(' '));
            Add("service-size", "CPU e memória numa combinação aceita pelo Fargate", Fargate(service.Cpu, service.Memory),
                "Ex.: 256/512, 512/1024, 1024/2048, 2048/4096.");
            Add("service-count", "Entre 1 e 10 tarefas", service.DesiredCount is >= 1 and <= 10);
            Add("load-balancer-subnets", "Ao menos duas subnets públicas, em zonas diferentes, para o load balancer",
                blueprint.Network.PublicSubnets.Count >= 2, "O load balancer exige duas zonas de disponibilidade.");
            Add("certificate", $"Certificado ACM na região {blueprint.Region} para o HTTPS",
                Certificate().IsMatch(service.CertificateArn) && service.CertificateArn.StartsWith($"arn:aws:acm:{blueprint.Region}:", StringComparison.Ordinal),
                "O load balancer do modelo é só HTTPS: informe o ARN do certificado emitido na mesma região.");
        }

        if (blueprint.Database is { } database)
        {
            Add("database-engine", "Banco postgres ou mysql", database.Engine is "postgres" or "mysql");
            Add("database-version", "Versão principal do banco (ex.: 16)", MajorVersion().IsMatch(database.EngineVersion));
            Add("database-class", "Classe de instância RDS (ex.: db.t4g.micro)", InstanceClass().IsMatch(database.InstanceClass));
            Add("database-storage", "Armazenamento entre 20 e 1000 GB", database.StorageGb is >= 20 and <= 1000);
            Add("database-subnets", "Ao menos duas subnets privadas para o grupo de subnets do banco", blueprint.Network.PrivateSubnets.Count >= 2,
                "O RDS exige subnets em duas zonas de disponibilidade.");
        }

        Add("queues", $"Até {MaxQueues} filas, nomes em minúsculas, dígitos e hífen, sem repetir",
            blueprint.Queues.Count <= MaxQueues && blueprint.Queues.All(Queue().IsMatch) && blueprint.Queues.Distinct(StringComparer.Ordinal).Count() == blueprint.Queues.Count);
        Add("components", "Ao menos um componente: serviço, banco ou fila",
            blueprint.Service is not null || blueprint.Database is not null || blueprint.Queues.Count > 0);
        Add("alarm-email", "E-mail de alarme válido, quando informado", blueprint.AlarmEmail is null || Email().IsMatch(blueprint.AlarmEmail));
        if (blueprint.Backend is { } backend)
            Add("backend", "Bucket de state e região válidos", S3Bucket().IsMatch(backend.Bucket) && Region().IsMatch(backend.Region));
        return checks;
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

    [GeneratedRegex("^[a-z][a-z0-9-]*[a-z0-9]$")]
    private static partial Regex Project();

    [GeneratedRegex("^[a-z]{2}(-gov)?-[a-z]+-[0-9]$")]
    private static partial Regex Region();

    [GeneratedRegex("^[0-9]{12}$")]
    private static partial Regex Account();

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,64}$")]
    private static partial Regex Profile();

    [GeneratedRegex("^vpc-[0-9a-f]{8,17}$")]
    private static partial Regex Vpc();

    [GeneratedRegex("^subnet-[0-9a-f]{8,17}$")]
    private static partial Regex Subnet();

    [GeneratedRegex("^[a-z][a-z0-9-]{0,9}$")]
    private static partial Regex Host();

    [GeneratedRegex("^arn:aws:acm:[a-z0-9-]+:[0-9]{12}:certificate/[0-9a-f-]{36}$")]
    private static partial Regex Certificate();

    [GeneratedRegex("^[0-9]{1,2}$")]
    private static partial Regex MajorVersion();

    [GeneratedRegex(@"^db\.[a-z0-9]+\.[a-z0-9]+$")]
    private static partial Regex InstanceClass();

    [GeneratedRegex("^[a-z][a-z0-9-]{1,38}[a-z0-9]$")]
    private static partial Regex Queue();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Email();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    private static partial Regex S3Bucket();
}
