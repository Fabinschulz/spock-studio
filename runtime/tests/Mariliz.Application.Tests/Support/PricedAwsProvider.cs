using Mariliz.Application.Cloud;

namespace Mariliz.Application.Tests.Support;

// Um provedor "aws" de teste: as capacidades que o vínculo usa e a lista de preços de sa-east-1.
internal sealed class PricedAwsProvider : ICloudProvider, ICloudPriceSource
{
    private static readonly Dictionary<string, decimal> Prices = new(StringComparer.Ordinal)
    {
        ["LoadBalancerUsage"] = 0.034m,
        ["Fargate-vCPU-Hours:perCPU"] = 0.0696m,
        ["Fargate-GB-Hours"] = 0.0076m,
        ["AWSSecretsManager-Secrets"] = 0.40m,
        ["InstanceUsage:db.t4g.micro"] = 0.034m,
        ["RDS:GP3-Storage"] = 0.219m,
        ["CW:AlarmMonitorUsage"] = 0.135m,
        ["LCUUsage"] = 0.011m,
        ["TimedStorage-ByteHrs"] = 0.10m,
        ["DataProcessing-Bytes"] = 0.90m,
        ["Requests-Tier1"] = 0.0000004m,
        ["BoxUsage:t3.micro"] = 0.0168m,
        ["NatGateway-Hours"] = 0.093m,
        ["InstanceUsage:db.t4g.small"] = 0.068m
    };

    private static readonly CloudColumn[] Columns = [new("name", "Nome"), new("value", "Valor")];

    public List<CloudPriceQuery> Queries { get; } = [];
    public HashSet<string> Missing { get; } = [];

    public CloudProviderDescriptor Descriptor { get; } = new("aws", "AWS de teste", "1.0.0",
        [new CloudMechanism("simple", "Simples", "Teste.", [new CloudField("region", "Região", "", Required: true)])],
        [
            new CloudCapability("aws.ec2.vpcs", "VPCs", "VPCs.", "EC2", ["ec2:DescribeVpcs"], CloudRegionUse.Required, Columns),
            new CloudCapability("aws.pricing", "Preços públicos", "Preços.", "Price List", ["pricing:GetProducts"], CloudRegionUse.None, Columns)
        ],
        "Teste.");

    public CloudSettings Normalize(CloudSettings settings) => settings;

    public string NormalizeRegion(string region) => region;

    public Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct) =>
        Task.FromResult(new CloudIdentity("123456789012", null, "arn:teste", null));

    public Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct) => Task.FromResult(new CloudTable([], false));

    public Task<IReadOnlyList<CloudPriceQuote>> PricesAsync(CloudSettings settings, IReadOnlyList<CloudPriceQuery> queries, CancellationToken ct)
    {
        Queries.AddRange(queries);
        IReadOnlyList<CloudPriceQuote> quotes = queries
            .Select(q => Missing.Contains(q.UsageType) || !Prices.TryGetValue(q.UsageType, out var price)
                ? new CloudPriceQuote(q, null, null)
                : new CloudPriceQuote(q, price, $"{price.ToString(System.Globalization.CultureInfo.InvariantCulture)} USD por {q.Unit}"))
            .ToList();
        return Task.FromResult(quotes);
    }
}
