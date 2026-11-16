using System.Globalization;
using Mariliz.Application.Cloud;

namespace Mariliz.Application.Iac;

// Estimativa de custo de um desenho da AWS com os preços públicos sob demanda que a própria AWS publica. O que depende de
// tráfego (requisições, GB de log, LCU) aparece com o preço da unidade e fica fora do total: o Spock não inventa uso.
public static class IacCostEstimator
{
    public const decimal HoursPerMonth = 730m;
    public const string Currency = "USD";
    public const string Source = "AWS Price List Query API";

    // Scale converte o preço da lista para a unidade mostrada (por requisição vira por milhão de requisições).
    public sealed record Item(string Resource, string Basis, decimal? Quantity, string Unit, CloudPriceQuery Query, decimal Scale = 1m);

    public static IReadOnlyList<Item> Items(IacBlueprint b)
    {
        var region = b.Region;
        var items = new List<Item>();
        CloudPriceQuery Q(string service, string usage, string unit, params (string Key, string Value)[] attributes) =>
            new(service, region, attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal), usage, unit);
        var alb = new[] { ("productFamily", "Load Balancer-Application"), ("locationType", "AWS Region") };

        if (b.Service is { } s)
        {
            var vcpu = s.Cpu / 1024m;
            var gb = s.Memory / 1024m;
            items.Add(new("Application Load Balancer", "Ligado o mês todo", HoursPerMonth, "hora", Q("AWSELB", "LoadBalancerUsage", "Hrs", alb)));
            items.Add(new("ECS Fargate, CPU", $"{s.DesiredCount} tarefa(s) de {Text(vcpu)} vCPU o mês todo", s.DesiredCount * vcpu * HoursPerMonth, "vCPU-hora",
                Q("AmazonECS", "Fargate-vCPU-Hours:perCPU", "hours", ("cputype", "perCPU"))));
            items.Add(new("ECS Fargate, memória", $"{s.DesiredCount} tarefa(s) de {Text(gb)} GB o mês todo", s.DesiredCount * gb * HoursPerMonth, "GB-hora",
                Q("AmazonECS", "Fargate-GB-Hours", "hours", ("memorytype", "perGB"))));
        }
        var secrets = (b.Service is null ? 0 : 1) + (b.Database is null ? 0 : 1);
        if (secrets > 0)
            items.Add(new("Secrets Manager", b.Database is null ? "Segredo da aplicação" : b.Service is null ? "Senha do banco, guardada pelo RDS" : "Segredo da aplicação e senha do banco",
                secrets, "segredo-mês", Q("AWSSecretsManager", "AWSSecretsManager-Secrets", "Secrets", ("group", "AWSSecretsManager-Secret"))));
        if (b.Database is { } d)
        {
            var engine = d.Engine == "mysql" ? "MySQL" : "PostgreSQL";
            var deployment = d.MultiAz ? "Multi-AZ" : "Single-AZ";
            items.Add(new($"RDS {d.Engine} {d.InstanceClass}", $"{deployment}, ligado o mês todo", HoursPerMonth, "hora",
                Q("AmazonRDS", (d.MultiAz ? "Multi-AZUsage:" : "InstanceUsage:") + d.InstanceClass, "Hrs", ("productFamily", "Database Instance"),
                    ("databaseEngine", engine), ("instanceType", d.InstanceClass), ("deploymentOption", deployment))));
            items.Add(new("RDS, armazenamento gp3", $"{d.StorageGb} GB {deployment}", d.StorageGb, "GB-mês",
                Q("AmazonRDS", d.MultiAz ? "RDS:Multi-AZ-GP3-Storage" : "RDS:GP3-Storage", "GB-Mo", ("productFamily", "Database Storage"),
                    ("databaseEngine", engine), ("deploymentOption", deployment), ("volumeType", "General Purpose-GP3"))));
        }
        var alarms = b.Queues.Count + (b.Service is null ? 0 : 1) + (b.Database is null ? 0 : 1);
        if (alarms > 0)
            items.Add(new("CloudWatch Alarms", $"{alarms} alarme(s) de resolução padrão", alarms, "alarme-mês",
                Q("AmazonCloudWatch", "CW:AlarmMonitorUsage", "Alarms", ("productFamily", "Alarm"))));

        if (b.Service is not null)
        {
            items.Add(new("Application Load Balancer, capacidade", "Depende do tráfego", null, "LCU-hora", Q("AWSELB", "LCUUsage", "LCU-Hrs", alb)));
            items.Add(new("ECR, imagens", "Depende do tamanho das imagens (guarda as 20 mais recentes)", null, "GB-mês",
                Q("AmazonECR", "TimedStorage-ByteHrs", "GB-Mo")));
            items.Add(new("CloudWatch Logs, ingestão", "Depende do volume de log do serviço", null, "GB",
                Q("AmazonCloudWatch", "DataProcessing-Bytes", "GB", ("productFamily", "Data Payload"), ("group", "Ingested Logs"))));
        }
        if (b.Queues.Count > 0)
            items.Add(new("SQS", "Depende do volume de mensagens", null, "milhão de requisições",
                Q("AWSQueueService", "Requests-Tier1", "Requests", ("queueType", "Standard")), 1_000_000m));
        return items;
    }

    public static IacCostEstimate Compose(IacBlueprint b, IReadOnlyList<Item> items, IReadOnlyList<CloudPriceQuote> quotes, DateTimeOffset pricedAt)
    {
        var lines = items.Zip(quotes, (item, quote) =>
        {
            var unit = quote.UsdPerUnit * item.Scale;
            var monthly = item.Quantity is { } quantity && unit is { } price ? decimal.Round(quantity * price, 2) : (decimal?)null;
            return new IacCostLine(item.Resource, item.Basis, item.Quantity, item.Unit, unit, monthly, quote.Description);
        }).ToList();
        var fixedLines = lines.Where(l => l.Quantity is not null).ToList();
        var usage = lines.Where(l => l.Quantity is null).ToList();

        var assumptions = new List<string>
        {
            $"Estimativa com os preços públicos sob demanda da AWS em {b.Region}, em dólares, consultados na {Source} em {pricedAt.UtcDateTime:yyyy-MM-dd}.",
            "Mês de 730 horas, com tudo ligado o mês inteiro.",
            "Sem impostos, câmbio, free tier, Savings Plans, instâncias reservadas nem descontos da conta.",
            "Os itens por uso dependem do tráfego e ficam fora do total.",
            "Transferência de dados para a internet e backup do banco além do tamanho dele não entram."
        };
        if (b.Service is { PublicIp: false })
            assumptions.Add("Tarefas sem IP público precisam de NAT Gateway ou endpoints VPC, que não estão nesta configuração nem na estimativa.");
        var missing = fixedLines.Where(l => l.UnitPrice is null).Select(l => l.Resource).ToList();
        if (missing.Count > 0)
            assumptions.Add("Sem preço único na lista da AWS para: " + string.Join(", ", missing) + ". O total não inclui esses itens.");

        return new IacCostEstimate(Currency, b.Region, Source, pricedAt, fixedLines.Sum(l => l.Monthly ?? 0m), missing.Count == 0, fixedLines, usage,
            assumptions);
    }

    private static string Text(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
