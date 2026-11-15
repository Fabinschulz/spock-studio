using System.Globalization;
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.RDS;
using Amazon.RDS.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Mariliz.Application.Cloud;

namespace Mariliz.Infrastructure.Cloud.Aws;

internal static class AwsClients
{
    public static T Config<T>(string region) where T : ClientConfig, new() => new()
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        Timeout = TimeSpan.FromSeconds(15),
        MaxErrorRetry = 2
    };
}

// Leituras com mais valor para engenharia e diagnóstico. Cada uma pede ao provedor no máximo limite + 1 itens, para saber
// se há mais sem baixar o resto, e para depois de MaxPages páginas.
internal static class AwsReaders
{
    private const int MaxPages = 10;

    private static CloudColumn C(string id, string label) => new(id, label);

    public static readonly IReadOnlyList<CloudCapability> Capabilities =
    [
        new("aws.regions", "Regiões", "Regiões da conta e se estão habilitadas.", "EC2", ["ec2:DescribeRegions"], CloudRegionUse.None,
            [C("region", "Região"), C("optIn", "Habilitação"), C("endpoint", "Endpoint")]),
        new("aws.ec2.instances", "Instâncias EC2", "Máquinas virtuais da região, com estado e tipo.", "EC2", ["ec2:DescribeInstances"], CloudRegionUse.Required,
            [C("id", "Id"), C("name", "Nome"), C("type", "Tipo"), C("state", "Estado"), C("zone", "Zona"), C("privateIp", "IP privado"), C("launchedAt", "Iniciada em")]),
        new("aws.ec2.vpcs", "VPCs", "Redes virtuais da região, com faixa de endereços e se é a padrão.", "EC2", ["ec2:DescribeVpcs"], CloudRegionUse.Required,
            [C("id", "Id"), C("name", "Nome"), C("cidr", "Faixa"), C("default", "Padrão"), C("state", "Estado")]),
        new("aws.ec2.subnets", "Subnets", "Subnets da região, com zona e se saem para a internet por um internet gateway (pública).", "EC2",
            ["ec2:DescribeSubnets", "ec2:DescribeRouteTables"], CloudRegionUse.Required,
            [C("id", "Id"), C("name", "Nome"), C("vpc", "VPC"), C("zone", "Zona"), C("cidr", "Faixa"), C("public", "Pública"), C("freeIps", "IPs livres")]),
        new("aws.ecs.clusters", "Clusters ECS", "Clusters de contêineres da região, com tarefas e serviços.", "ECS", ["ecs:ListClusters", "ecs:DescribeClusters"], CloudRegionUse.Required,
            [C("name", "Nome"), C("status", "Estado"), C("runningTasks", "Tarefas em execução"), C("services", "Serviços ativos"), C("instances", "Instâncias registradas")]),
        new("aws.ecr.repositories", "Repositórios ECR", "Registros de imagens de contêiner da região.", "ECR", ["ecr:DescribeRepositories"], CloudRegionUse.Required,
            [C("name", "Nome"), C("uri", "URI"), C("createdAt", "Criado em"), C("tagMutability", "Tags"), C("scanOnPush", "Varredura no push")]),
        new("aws.rds.instances", "Bancos RDS", "Instâncias de banco de dados gerenciado da região.", "RDS", ["rds:DescribeDBInstances"], CloudRegionUse.Required,
            [C("id", "Id"), C("engine", "Motor"), C("version", "Versão"), C("class", "Classe"), C("status", "Estado"), C("multiAz", "Multi-AZ"), C("zone", "Zona")]),
        new("aws.s3.buckets", "Buckets S3", "Buckets da conta, com a região de cada um quando a AWS informa.", "S3", ["s3:ListAllMyBuckets"], CloudRegionUse.None,
            [C("name", "Nome"), C("createdAt", "Criado em"), C("region", "Região")]),
        new("aws.lambda.functions", "Funções Lambda", "Funções da região, com runtime, memória e tempo limite.", "Lambda", ["lambda:ListFunctions"], CloudRegionUse.Required,
            [C("name", "Nome"), C("runtime", "Runtime"), C("memoryMb", "Memória (MB)"), C("timeoutS", "Tempo limite (s)"), C("modifiedAt", "Alterada em")]),
        new("aws.cloudformation.stacks", "Pilhas CloudFormation", "Pilhas de infraestrutura da região, com estado e desvio.", "CloudFormation", ["cloudformation:DescribeStacks"], CloudRegionUse.Required,
            [C("name", "Nome"), C("status", "Estado"), C("createdAt", "Criada em"), C("updatedAt", "Atualizada em"), C("drift", "Desvio")]),
        new("aws.cloudwatch.alarms", "Alarmes CloudWatch", "Alarmes de métrica da região e o estado atual de cada um.", "CloudWatch", ["cloudwatch:DescribeAlarms"], CloudRegionUse.Required,
            [C("name", "Nome"), C("state", "Estado"), C("metric", "Métrica"), C("namespace", "Namespace"), C("changedAt", "Mudou em")])
    ];

    public static Task<CloudTable> ReadAsync(AWSCredentials credentials, string region, CloudQuery query, CancellationToken ct) => query.Capability switch
    {
        "aws.regions" => RegionsAsync(credentials, region, query.Limit, ct),
        "aws.ec2.instances" => InstancesAsync(credentials, region, query.Limit, ct),
        "aws.ec2.vpcs" => VpcsAsync(credentials, region, query.Limit, ct),
        "aws.ec2.subnets" => SubnetsAsync(credentials, region, query.Limit, ct),
        "aws.ecs.clusters" => ClustersAsync(credentials, region, query.Limit, ct),
        "aws.ecr.repositories" => RepositoriesAsync(credentials, region, query.Limit, ct),
        "aws.rds.instances" => DatabasesAsync(credentials, region, query.Limit, ct),
        "aws.s3.buckets" => BucketsAsync(credentials, region, query.Limit, ct),
        "aws.lambda.functions" => FunctionsAsync(credentials, region, query.Limit, ct),
        "aws.cloudformation.stacks" => StacksAsync(credentials, region, query.Limit, ct),
        "aws.cloudwatch.alarms" => AlarmsAsync(credentials, region, query.Limit, ct),
        _ => throw CloudErrors.CapabilityUnknown()
    };

    private static async Task<CloudTable> RegionsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ec2 = new AmazonEC2Client(credentials, AwsClients.Config<AmazonEC2Config>(region));
        var response = await ec2.DescribeRegionsAsync(new DescribeRegionsRequest { AllRegions = true }, ct);
        var rows = (response.Regions ?? []).OrderBy(r => r.RegionName, StringComparer.Ordinal)
            .Select(r => Row(r.RegionName, r.OptInStatus, r.Endpoint)).ToList();
        return Table(rows, limit);
    }

    private static async Task<CloudTable> InstancesAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ec2 = new AmazonEC2Client(credentials, AwsClients.Config<AmazonEC2Config>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await ec2.DescribeInstancesAsync(new DescribeInstancesRequest { MaxResults = Math.Clamp(limit + 1, 5, 1000), NextToken = token }, ct);
            foreach (var instance in (response.Reservations ?? []).SelectMany(r => r.Instances ?? []))
            {
                var name = (instance.Tags ?? []).FirstOrDefault(t => t.Key == "Name")?.Value;
                rows.Add(Row(instance.InstanceId, name, instance.InstanceType?.Value, instance.State?.Name?.Value, instance.Placement?.AvailabilityZone,
                    instance.PrivateIpAddress, Date(instance.LaunchTime)));
            }
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static async Task<CloudTable> VpcsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ec2 = new AmazonEC2Client(credentials, AwsClients.Config<AmazonEC2Config>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await ec2.DescribeVpcsAsync(new DescribeVpcsRequest { MaxResults = Math.Clamp(limit + 1, 5, 1000), NextToken = token }, ct);
            rows.AddRange((response.Vpcs ?? []).Select(v => Row(v.VpcId, Name(v.Tags), v.CidrBlock, Flag(v.IsDefault), v.State?.Value)));
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    // Pública é a subnet cuja tabela de rotas (a associada a ela ou, sem associação, a principal da VPC) manda 0.0.0.0/0
    // para um internet gateway. MapPublicIpOnLaunch sozinho não diz isso.
    private static async Task<CloudTable> SubnetsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ec2 = new AmazonEC2Client(credentials, AwsClients.Config<AmazonEC2Config>(region));
        var subnets = new List<Amazon.EC2.Model.Subnet>();
        string? token = null;
        for (var page = 0; page < MaxPages && subnets.Count <= limit; page++)
        {
            var response = await ec2.DescribeSubnetsAsync(new DescribeSubnetsRequest { MaxResults = Math.Clamp(limit + 1, 5, 1000), NextToken = token }, ct);
            subnets.AddRange(response.Subnets ?? []);
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }

        var tables = new List<RouteTable>();
        string? routesToken = null;
        for (var page = 0; page < MaxPages; page++)
        {
            var response = await ec2.DescribeRouteTablesAsync(new DescribeRouteTablesRequest { MaxResults = 100, NextToken = routesToken }, ct);
            tables.AddRange(response.RouteTables ?? []);
            routesToken = response.NextToken;
            if (string.IsNullOrEmpty(routesToken)) break;
        }
        static bool ToInternet(RouteTable table) => (table.Routes ?? []).Any(r =>
            r.DestinationCidrBlock == "0.0.0.0/0" && r.GatewayId?.StartsWith("igw-", StringComparison.Ordinal) == true);
        var bySubnet = tables.SelectMany(t => (t.Associations ?? []).Where(a => a.SubnetId is not null).Select(a => (a.SubnetId, Table: t)))
            .GroupBy(x => x.SubnetId).ToDictionary(g => g.Key, g => g.First().Table, StringComparer.Ordinal);
        var main = tables.Where(t => (t.Associations ?? []).Any(a => a.Main == true)).GroupBy(t => t.VpcId)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        string? Public(Amazon.EC2.Model.Subnet subnet)
        {
            var table = bySubnet.GetValueOrDefault(subnet.SubnetId) ?? main.GetValueOrDefault(subnet.VpcId);
            return table is null ? null : Flag(ToInternet(table));
        }

        var rows = subnets.OrderBy(s => s.VpcId, StringComparer.Ordinal).ThenBy(s => s.AvailabilityZone, StringComparer.Ordinal)
            .Select(s => Row(s.SubnetId, Name(s.Tags), s.VpcId, s.AvailabilityZone, s.CidrBlock, Public(s), Number(s.AvailableIpAddressCount))).ToList();
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static string? Name(List<Amazon.EC2.Model.Tag>? tags) => (tags ?? []).FirstOrDefault(t => t.Key == "Name")?.Value;

    private static async Task<CloudTable> ClustersAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ecs = new AmazonECSClient(credentials, AwsClients.Config<AmazonECSConfig>(region));
        var arns = new List<string>();
        string? token = null;
        for (var page = 0; page < MaxPages && arns.Count <= limit; page++)
        {
            var response = await ecs.ListClustersAsync(new ListClustersRequest { MaxResults = Math.Clamp(limit + 1, 1, 100), NextToken = token }, ct);
            arns.AddRange(response.ClusterArns ?? []);
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        var rows = new List<IReadOnlyList<string?>>();
        foreach (var batch in arns.Take(limit + 1).Chunk(100))
        {
            var described = await ecs.DescribeClustersAsync(new DescribeClustersRequest { Clusters = [.. batch] }, ct);
            rows.AddRange((described.Clusters ?? []).Select(c => Row(c.ClusterName, c.Status, Number(c.RunningTasksCount), Number(c.ActiveServicesCount),
                Number(c.RegisteredContainerInstancesCount))));
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static async Task<CloudTable> RepositoriesAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var ecr = new AmazonECRClient(credentials, AwsClients.Config<AmazonECRConfig>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await ecr.DescribeRepositoriesAsync(new DescribeRepositoriesRequest { MaxResults = Math.Clamp(limit + 1, 1, 1000), NextToken = token }, ct);
            rows.AddRange((response.Repositories ?? []).Select(r => Row(r.RepositoryName, r.RepositoryUri, Date(r.CreatedAt), r.ImageTagMutability?.Value,
                Flag(r.ImageScanningConfiguration?.ScanOnPush))));
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static async Task<CloudTable> DatabasesAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var rds = new AmazonRDSClient(credentials, AwsClients.Config<AmazonRDSConfig>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? marker = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await rds.DescribeDBInstancesAsync(new DescribeDBInstancesRequest { MaxRecords = Math.Clamp(limit + 1, 20, 100), Marker = marker }, ct);
            rows.AddRange((response.DBInstances ?? []).Select(d => Row(d.DBInstanceIdentifier, d.Engine, d.EngineVersion, d.DBInstanceClass, d.DBInstanceStatus,
                Flag(d.MultiAZ), d.AvailabilityZone)));
            marker = response.Marker;
            if (string.IsNullOrEmpty(marker)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(marker));
    }

    private static async Task<CloudTable> BucketsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var s3 = new AmazonS3Client(credentials, AwsClients.Config<AmazonS3Config>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await s3.ListBucketsAsync(new ListBucketsRequest { MaxBuckets = Math.Clamp(limit + 1, 1, 10000), ContinuationToken = token }, ct);
            rows.AddRange((response.Buckets ?? []).Select(b => Row(b.BucketName, Date(b.CreationDate), b.BucketRegion)));
            token = response.ContinuationToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static async Task<CloudTable> FunctionsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var lambda = new AmazonLambdaClient(credentials, AwsClients.Config<AmazonLambdaConfig>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? marker = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await lambda.ListFunctionsAsync(new ListFunctionsRequest { MaxItems = Math.Clamp(limit + 1, 1, 50), Marker = marker }, ct);
            rows.AddRange((response.Functions ?? []).Select(f => Row(f.FunctionName, f.Runtime?.Value, Number(f.MemorySize), Number(f.Timeout), f.LastModified)));
            marker = response.NextMarker;
            if (string.IsNullOrEmpty(marker)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(marker));
    }

    private static async Task<CloudTable> StacksAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var cloudFormation = new AmazonCloudFormationClient(credentials, AwsClients.Config<AmazonCloudFormationConfig>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await cloudFormation.DescribeStacksAsync(new DescribeStacksRequest { NextToken = token }, ct);
            rows.AddRange((response.Stacks ?? []).Select(s => Row(s.StackName, s.StackStatus?.Value, Date(s.CreationTime), Date(s.LastUpdatedTime),
                s.DriftInformation?.StackDriftStatus?.Value)));
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static async Task<CloudTable> AlarmsAsync(AWSCredentials credentials, string region, int limit, CancellationToken ct)
    {
        using var cloudWatch = new AmazonCloudWatchClient(credentials, AwsClients.Config<AmazonCloudWatchConfig>(region));
        var rows = new List<IReadOnlyList<string?>>();
        string? token = null;
        for (var page = 0; page < MaxPages && rows.Count <= limit; page++)
        {
            var response = await cloudWatch.DescribeAlarmsAsync(new DescribeAlarmsRequest { MaxRecords = Math.Clamp(limit + 1, 1, 100), NextToken = token }, ct);
            rows.AddRange((response.MetricAlarms ?? []).Select(a => Row(a.AlarmName, a.StateValue?.Value, a.MetricName, a.Namespace, Date(a.StateUpdatedTimestamp))));
            token = response.NextToken;
            if (string.IsNullOrEmpty(token)) break;
        }
        return Table(rows, limit, more: !string.IsNullOrEmpty(token));
    }

    private static CloudTable Table(List<IReadOnlyList<string?>> rows, int limit, bool more = false) =>
        new(rows.Take(limit).ToList(), more || rows.Count > limit);

    private static IReadOnlyList<string?> Row(params string?[] values) => values;

    private static string? Date(DateTime? value) =>
        value?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? Number(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Flag(bool? value) => value switch { true => "sim", false => "não", null => null };
}
