using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Cloud.Aws;

namespace Mariliz.Application.Tests;

// Entrega 6: drift lido do resource_drift de um plano refresh-only (só nomes, nunca valores) e custo estimado com a lista
// pública de preços do provedor, pela conexão do projeto. Os preços de mentira são os de sa-east-1 publicados pela AWS.
public sealed class IacDriftAndCostTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly PricedProvider _provider = new();

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private CloudHub Hub() => new(new FileCloudStore(_project.DataDir), new CloudProviderRegistry([_provider]), Workspace(), _clock, _audit);

    [Fact]
    public void Drift_le_alterados_e_apagados_com_os_nomes_dos_atributos_e_nunca_os_valores()
    {
        var plan = JsonNode.Parse("""
            {
              "resource_drift": [
                { "address": "aws_sqs_queue.main[\"pedidos\"]", "mode": "managed", "type": "aws_sqs_queue",
                  "change": { "actions": ["update"],
                    "before": { "name": "loja-pedidos", "visibility_timeout_seconds": 180, "tags": { "Owner": "time" } },
                    "after":  { "name": "loja-pedidos", "visibility_timeout_seconds": 30, "tags": { "Owner": "outro-valor-secreto" } } } },
                { "address": "aws_sns_topic.alerts", "mode": "managed", "type": "aws_sns_topic",
                  "change": { "actions": ["delete"], "before": { "name": "loja-alertas" }, "after": null } },
                { "address": "data.aws_caller_identity.current", "mode": "data", "type": "aws_caller_identity",
                  "change": { "actions": ["update"], "before": { "id": "1" }, "after": { "id": "2" } } }
              ]
            }
            """)!;

        var drift = IacPlanReader.ReadDrift(plan);

        Assert.Equal(2, drift.Count);
        Assert.Equal("aws_sqs_queue.main[\"pedidos\"]", drift[0].Address);
        Assert.Equal("changed", drift[0].Change);
        Assert.Equal(["tags", "visibility_timeout_seconds"], drift[0].Attributes);
        Assert.Equal("deleted", drift[1].Change);
        Assert.Empty(drift[1].Attributes);
        Assert.DoesNotContain("outro-valor-secreto", System.Text.Json.JsonSerializer.Serialize(drift));
    }

    [Fact]
    public void Plano_sem_resource_drift_nao_tem_drift()
    {
        Assert.Empty(IacPlanReader.ReadDrift(JsonNode.Parse("""{ "resource_changes": [] }""")!));
    }

    [Fact]
    public async Task Custo_soma_os_itens_fixos_com_os_precos_da_lista_e_deixa_o_uso_fora_do_total()
    {
        var (hub, projectId, connectionId) = await Linked(capabilities: [CloudReadService.PricingCapability("aws")]);
        var service = new IacCostService(Workspace(), hub, _clock);

        var estimate = await service.EstimateAsync(new IacCostRequest(projectId, connectionId, IacBlueprintSamples.Full()), CloudCaller.Person, CancellationToken.None);

        Assert.Equal("USD", estimate.Currency);
        Assert.True(estimate.Complete);
        Assert.Equal(117.27m, estimate.MonthlyFixed);
        var fargate = estimate.Fixed.Single(l => l.Resource == "ECS Fargate, CPU");
        Assert.Equal(730m, fargate.Quantity);
        Assert.Equal(50.81m, fargate.Monthly);
        Assert.Equal(0.54m, estimate.Fixed.Single(l => l.Resource == "CloudWatch Alarms").Monthly);
        Assert.Equal(0.80m, estimate.Fixed.Single(l => l.Resource == "Secrets Manager").Monthly);
        Assert.All(estimate.UsageBased, line => Assert.Null(line.Monthly));
        Assert.Equal(0.40m, estimate.UsageBased.Single(l => l.Resource == "SQS").UnitPrice);
        Assert.Contains(estimate.Assumptions, a => a.Contains("NAT Gateway"));
        Assert.Contains(_audit.Entries, e => e.Action == "cloud.read" && e.Detail.Contains("\"capability\":\"aws.pricing\""));
        var instance = _provider.Queries.Single(q => q.UsageType == "InstanceUsage:db.t4g.micro");
        Assert.Equal("PostgreSQL", instance.Attributes["databaseEngine"]);
        Assert.Equal("Single-AZ", instance.Attributes["deploymentOption"]);
        Assert.All(_provider.Queries, q => Assert.Equal("sa-east-1", q.Region));
    }

    [Fact]
    public async Task Sem_preco_unico_o_item_fica_sem_valor_e_a_estimativa_se_diz_incompleta()
    {
        _provider.Missing.Add("InstanceUsage:db.t4g.micro");
        var (hub, projectId, connectionId) = await Linked(capabilities: [CloudReadService.PricingCapability("aws")]);

        var estimate = await new IacCostService(Workspace(), hub, _clock)
            .EstimateAsync(new IacCostRequest(projectId, connectionId, IacBlueprintSamples.Full()), CloudCaller.Person, CancellationToken.None);

        Assert.False(estimate.Complete);
        Assert.Null(estimate.Fixed.Single(l => l.Resource == "RDS postgres db.t4g.micro").Monthly);
        Assert.Equal(117.27m - 24.82m, estimate.MonthlyFixed);
        Assert.Contains(estimate.Assumptions, a => a.Contains("RDS postgres db.t4g.micro"));
    }

    [Fact]
    public async Task Custo_exige_conexao_vinculada_com_precos_liberados_e_desenho_valido()
    {
        var (hub, projectId, connectionId) = await Linked(capabilities: ["aws.ec2.vpcs"]);
        var service = new IacCostService(Workspace(), hub, _clock);

        var notEnabled = await Assert.ThrowsAsync<EngineException>(() =>
            service.EstimateAsync(new IacCostRequest(projectId, connectionId, IacBlueprintSamples.QueuesOnly()), CloudCaller.Person, CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<EngineException>(() => service.EstimateAsync(
            new IacCostRequest(projectId, connectionId, IacBlueprintSamples.QueuesOnly() with { Project = "Nome Inválido" }), CloudCaller.Person, CancellationToken.None));
        var noCloud = await Assert.ThrowsAsync<EngineException>(() =>
            new IacCostService(Workspace(), null, _clock).EstimateAsync(new IacCostRequest(projectId, connectionId, IacBlueprintSamples.QueuesOnly()),
                CloudCaller.Person, CancellationToken.None));

        Assert.Equal("CLOUD_CAPABILITY_NOT_ENABLED", notEnabled.Code);
        Assert.Equal("IAC_BLUEPRINT_INVALID", invalid.Code);
        Assert.Equal("IAC_COST_SOURCE_UNAVAILABLE", noCloud.Code);
        Assert.Empty(_provider.Queries);
    }

    [Fact]
    public void Desenho_so_com_filas_tem_alarmes_fixos_e_sqs_por_uso()
    {
        var items = IacCostEstimator.Items(IacBlueprintSamples.QueuesOnly());

        Assert.Equal(["CloudWatch Alarms", "SQS"], items.Select(i => i.Resource));
        Assert.Equal(1m, items[0].Quantity);
        Assert.Null(items[1].Quantity);
    }

    [Fact]
    public void Lista_da_aws_escolhe_o_tipo_de_uso_sem_o_prefixo_da_regiao_e_recusa_preco_ambiguo()
    {
        var query = new CloudPriceQuery("AWSELB", "sa-east-1",
            new Dictionary<string, string> { ["locationType"] = "AWS Region", ["productFamily"] = "Load Balancer-Application" }, "LoadBalancerUsage", "Hrs");
        string Product(string usage, string price, string location = "AWS Region") => $$"""
            { "product": { "productFamily": "Load Balancer-Application", "attributes": { "usagetype": "{{usage}}", "locationType": "{{location}}" } },
              "terms": { "OnDemand": { "X.Y": { "priceDimensions": { "X.Y.Z": { "unit": "Hrs", "beginRange": "0", "pricePerUnit": { "USD": "{{price}}" },
                "description": "{{price}} por hora" } } } } } }
            """;

        var picked = AwsPrices.Pick(query, [Product("SAE1-LoadBalancerUsage", "0.0340000000"), Product("SAE1-Outposts-LoadBalancerUsage", "0.9"),
            Product("SAE1-TS-LoadBalancerUsage", "0.0076"), Product("SAE1-LoadBalancerUsage", "0.5", "AWS Outposts")]);
        var plain = AwsPrices.Pick(query, [Product("LoadBalancerUsage", "0.0225")]);
        var ambiguous = AwsPrices.Pick(query, [Product("SAE1-LoadBalancerUsage", "0.034"), Product("SAE1-LoadBalancerUsage", "0.035")]);

        Assert.Equal(0.034m, picked.UsdPerUnit);
        Assert.Equal("0.0340000000 por hora", picked.Description);
        Assert.Equal(0.0225m, plain.UsdPerUnit);
        Assert.Null(ambiguous.UsdPerUnit);
    }

    private async Task<(CloudHub Hub, string ProjectId, string ConnectionId)> Linked(IReadOnlyList<string> capabilities)
    {
        var projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        var hub = Hub();
        var connection = await hub.Connections.CreateAsync("aws", "Conta de teste", "simple", new Dictionary<string, string> { ["region"] = "sa-east-1" },
            CancellationToken.None);
        hub.Projects.Link(projectId, connection.Id, capabilities, agentAccess: false);
        return (hub, projectId, connection.Id);
    }

    // Um provedor "aws" de teste: as capacidades que o vínculo usa e a lista de preços de sa-east-1.
    private sealed class PricedProvider : ICloudProvider, ICloudPriceSource
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
            ["Requests-Tier1"] = 0.0000004m
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
}
