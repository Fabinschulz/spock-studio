using System.Globalization;
using Mariliz.Application.Cloud;

namespace Mariliz.Application.Iac;

/// <param name="Before">Custo mensal antes do plano; null quando o recurso não existia ou não deu para cotar.</param>
/// <param name="After">Custo mensal depois do plano; null quando o recurso deixa de existir ou não deu para cotar.</param>
/// <param name="Problem">Por que um lado ficou sem valor (valor só conhecido depois do apply, tipo sem preço).</param>
public sealed record IacPlanCostLine(string Address, string Type, string Action, string Detail, decimal? Before, decimal? After, string? Problem);

/// <param name="Uncovered">Tipos que o plano muda e a estimativa não cota: sem custo fixo (IAM, security group) ou cobrados por uso.</param>
public sealed record IacPlanCostEstimate(
    string OperationId,
    string PlanId,
    string Currency,
    string Region,
    string Source,
    DateTimeOffset PricedAt,
    decimal MonthlyBefore,
    decimal MonthlyAfter,
    decimal MonthlyDelta,
    bool Complete,
    IReadOnlyList<IacPlanCostLine> Lines,
    IReadOnlyList<string> Uncovered,
    IReadOnlyList<string> Assumptions);

public sealed record IacPlanCostRequest(string ProjectId, string OperationId, string ConnectionId, string? Region);

// O custo fixo mensal do que um plano cria, muda ou destrói, com os preços públicos sob demanda da AWS. Cada recurso vira
// consultas de antes e de depois; o que depende de tráfego fica fora, como na estimativa do desenho.
public static class IacPlanCost
{
    public sealed record Part(CloudPriceQuery Query, decimal Quantity);

    // As partes de um lado do recurso, ou o motivo de não dar para cotar.
    public static (IReadOnlyList<Part> Parts, string? Problem) Parts(string type, IReadOnlyDictionary<string, string> values, string region)
    {
        if (AssumesSingleAz(type, values))
            values = new Dictionary<string, string>(values, StringComparer.Ordinal) { ["multi_az"] = "false" };
        if (values.Values.Contains(IacPlanBillables.Unknown))
            return ([], "valor conhecido só depois do apply: " + string.Join(", ", values.Where(v => v.Value == IacPlanBillables.Unknown).Select(v => v.Key)));
        string? V(string name) => values.TryGetValue(name, out var value) ? value : null;
        CloudPriceQuery Q(string service, string usage, string unit, params (string Key, string Value)[] attributes) =>
            new(service, region, attributes.ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal), usage, unit);
        var hours = IacCostEstimator.HoursPerMonth;

        switch (type)
        {
            case "aws_instance" when V("instance_type") is { } instance:
                return ([new Part(Q("AmazonEC2", "BoxUsage:" + instance, "Hrs", ("productFamily", "Compute Instance"), ("instanceType", instance),
                    ("operatingSystem", "Linux"), ("tenancy", "Shared"), ("preInstalledSw", "NA"), ("capacitystatus", "Used"),
                    ("licenseModel", "No License required")), hours)], null);
            case "aws_nat_gateway":
                return ([new Part(Q("AmazonEC2", "NatGateway-Hours", "Hrs", ("productFamily", "NAT Gateway")), hours)], null);
            case "aws_lb":
                return (V("load_balancer_type") ?? "application") switch
                {
                    "application" => ([new Part(Q("AWSELB", "LoadBalancerUsage", "Hrs", ("productFamily", "Load Balancer-Application"),
                        ("locationType", "AWS Region")), hours)], null),
                    "network" => ([new Part(Q("AWSELB", "LoadBalancerUsage", "Hrs", ("productFamily", "Load Balancer-Network"),
                        ("locationType", "AWS Region")), hours)], null),
                    var other => ([], $"load balancer {other} sem preço na estimativa")
                };
            case "aws_db_instance":
                return Database(V, Q, hours);
            case "aws_ecs_service":
                if (V("launch_type") is not "FARGATE") return ([], "só serviços Fargate são cotados; no EC2 o custo é das instâncias");
                if (!decimal.TryParse(V("desired_count"), NumberStyles.Number, CultureInfo.InvariantCulture, out var count)
                    || !decimal.TryParse(V("cpu"), NumberStyles.Number, CultureInfo.InvariantCulture, out var cpu)
                    || !decimal.TryParse(V("memory"), NumberStyles.Number, CultureInfo.InvariantCulture, out var memory))
                    return ([], "CPU, memória ou tarefas da task definition não estão no plano");
                return ([
                    new Part(Q("AmazonECS", "Fargate-vCPU-Hours:perCPU", "hours", ("cputype", "perCPU")), count * cpu / 1024m * hours),
                    new Part(Q("AmazonECS", "Fargate-GB-Hours", "hours", ("memorytype", "perGB")), count * memory / 1024m * hours)
                ], null);
            case "aws_secretsmanager_secret":
                return ([new Part(Q("AWSSecretsManager", "AWSSecretsManager-Secrets", "Secrets", ("group", "AWSSecretsManager-Secret")), 1m)], null);
            case "aws_cloudwatch_metric_alarm":
                return ([new Part(Q("AmazonCloudWatch", "CW:AlarmMonitorUsage", "Alarms", ("productFamily", "Alarm")), 1m)], null);
            default:
                return ([], "tipo sem preço na estimativa");
        }
    }

    private static (IReadOnlyList<Part>, string?) Database(Func<string, string?> v,
        Func<string, string, string, (string, string)[], CloudPriceQuery> q, decimal hours)
    {
        var engine = v("engine") switch
        {
            "postgres" => "PostgreSQL",
            "mysql" => "MySQL",
            "mariadb" => "MariaDB",
            var other => null
        };
        if (engine is null || v("instance_class") is not { } instance) return ([], $"motor {v("engine")} sem preço na estimativa");
        var multiAz = v("multi_az") == "true";
        var deployment = multiAz ? "Multi-AZ" : "Single-AZ";
        var parts = new List<Part>
        {
            new(q("AmazonRDS", (multiAz ? "Multi-AZUsage:" : "InstanceUsage:") + instance, "Hrs", [("productFamily", "Database Instance"),
                ("databaseEngine", engine), ("instanceType", instance), ("deploymentOption", deployment)]), hours)
        };
        if (!decimal.TryParse(v("allocated_storage"), NumberStyles.Number, CultureInfo.InvariantCulture, out var storage) || storage <= 0)
            return (parts, null);
        switch (v("storage_type"))
        {
            case "gp3":
                parts.Add(new(q("AmazonRDS", multiAz ? "RDS:Multi-AZ-GP3-Storage" : "RDS:GP3-Storage", "GB-Mo", [("productFamily", "Database Storage"),
                    ("databaseEngine", engine), ("deploymentOption", deployment), ("volumeType", "General Purpose-GP3")]), storage));
                break;
            // O gp2 aparece na lista também como "Any" motor, com o mesmo preço: o filtro fica sem o motor.
            case "gp2":
                parts.Add(new(q("AmazonRDS", multiAz ? "RDS:Multi-AZ-GP2-Storage" : "RDS:GP2-Storage", "GB-Mo", [("productFamily", "Database Storage"),
                    ("deploymentOption", deployment), ("volumeType", "General Purpose")]), storage));
                break;
            default:
                return (parts, $"armazenamento {v("storage_type") ?? "sem tipo"} sem preço na estimativa");
        }
        return (parts, null);
    }

    // multi_az não declarado chega ao plano como valor calculado, mas a AWS cria Single-AZ quando ele falta.
    public static bool AssumesSingleAz(string type, IReadOnlyDictionary<string, string>? values) =>
        type == "aws_db_instance" && values is not null && values.TryGetValue("multi_az", out var multiAz) && multiAz == IacPlanBillables.Unknown;

    public static string Detail(IacBillable billable)
    {
        var values = billable.After ?? billable.Before ?? new Dictionary<string, string>();
        string? V(string name) => values.TryGetValue(name, out var value) && value != IacPlanBillables.Unknown ? value : null;
        return billable.Type switch
        {
            "aws_instance" => $"EC2 {V("instance_type") ?? "?"} (Linux)",
            "aws_nat_gateway" => "NAT Gateway",
            "aws_lb" => $"Load balancer {V("load_balancer_type") ?? "application"}",
            "aws_db_instance" => $"RDS {V("engine") ?? "?"} {V("instance_class") ?? "?"}, {(V("multi_az") == "true" ? "Multi-AZ" : "Single-AZ")}, " +
                                 $"{V("allocated_storage") ?? "?"} GB {V("storage_type") ?? ""}".TrimEnd(),
            "aws_ecs_service" => $"ECS {V("launch_type") ?? "?"}, {V("desired_count") ?? "?"} tarefa(s) de {V("cpu") ?? "?"} CPU e {V("memory") ?? "?"} MB",
            "aws_secretsmanager_secret" => "Segredo do Secrets Manager",
            "aws_cloudwatch_metric_alarm" => "Alarme do CloudWatch",
            _ => billable.Type
        };
    }
}
