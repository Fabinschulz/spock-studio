using System.Text.Json.Nodes;
using Mariliz.Application.Cloud;

namespace Mariliz.Application.Iac;

/// <param name="Capability">A leitura da conexão que achou o recurso.</param>
public sealed record IacUnmanagedResource(string Type, string Capability, string Id, string? Name);

// O que existe na conta e não está no state, para os tipos que o projeto já gerencia. Do state saem só identificadores
// (id, nome, identifier) dos tipos da tabela; nenhum outro atributo é lido.
public static class IacUnmanaged
{
    /// <param name="StateAttribute">O atributo do state que guarda o mesmo identificador que a leitura mostra.</param>
    /// <param name="IdColumn">A coluna da leitura com esse identificador.</param>
    /// <param name="Skip">Linhas que nunca contam (a VPC padrão, bucket de outra região).</param>
    public sealed record Mapping(string Type, string Capability, string StateAttribute, string IdColumn, string? NameColumn,
        Func<IReadOnlyDictionary<string, string?>, string?, bool>? Skip = null);

    public static readonly IReadOnlyList<Mapping> Aws =
    [
        new("aws_instance", "aws.ec2.instances", "id", "id", "name", (row, _) => row.GetValueOrDefault("state") is "terminated" or "shutting-down"),
        new("aws_vpc", "aws.ec2.vpcs", "id", "id", "name", (row, _) => row.GetValueOrDefault("default") == CloudFlags.True),
        new("aws_subnet", "aws.ec2.subnets", "id", "id", "name"),
        new("aws_db_instance", "aws.rds.instances", "identifier", "id", null),
        new("aws_ecr_repository", "aws.ecr.repositories", "name", "name", null),
        new("aws_ecs_cluster", "aws.ecs.clusters", "name", "name", null),
        new("aws_lambda_function", "aws.lambda.functions", "function_name", "name", null),
        new("aws_cloudwatch_metric_alarm", "aws.cloudwatch.alarms", "alarm_name", "name", null),
        // Bucket é da conta inteira: só os da região da conexão entram na comparação.
        new("aws_s3_bucket", "aws.s3.buckets", "bucket", "name", null, (row, region) => region is not null && row.GetValueOrDefault("region") is { } r && r != region)
    ];

    // Identificadores por tipo, do state que o plano leu (prior_state), em todos os módulos.
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> StateIds(JsonNode plan)
    {
        var attributes = Aws.ToDictionary(m => m.Type, m => m.StateAttribute, StringComparer.Ordinal);
        var ids = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Walk(JsonObject? module)
        {
            if (module is null) return;
            foreach (var resource in (module["resources"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (Text(resource["mode"]) != "managed" || Text(resource["type"]) is not { } type || !attributes.TryGetValue(type, out var attribute)) continue;
                if (Text(resource["values"]?[attribute]) is { Length: > 0 } id)
                {
                    if (!ids.TryGetValue(type, out var set)) ids[type] = set = new HashSet<string>(StringComparer.Ordinal);
                    set.Add(id);
                }
            }
            foreach (var child in (module["child_modules"] as JsonArray ?? []).OfType<JsonObject>()) Walk(child);
        }
        Walk(plan["prior_state"]?["values"]?["root_module"] as JsonObject);
        return ids.ToDictionary(p => p.Key, p => (IReadOnlySet<string>)p.Value, StringComparer.Ordinal);
    }

    public static IReadOnlyList<IacUnmanagedResource> Compare(Mapping mapping, IReadOnlySet<string> managed, CloudReadResult read, string? region)
    {
        var columns = read.Columns.Select(c => c.Id).ToList();
        var result = new List<IacUnmanagedResource>();
        foreach (var values in read.Rows)
        {
            var row = columns.Select((column, i) => (column, value: i < values.Count ? values[i] : null))
                .ToDictionary(p => p.column, p => p.value, StringComparer.Ordinal);
            if (row.GetValueOrDefault(mapping.IdColumn) is not { Length: > 0 } id || managed.Contains(id)) continue;
            if (mapping.Skip?.Invoke(row, read.Region ?? region) == true) continue;
            result.Add(new IacUnmanagedResource(mapping.Type, mapping.Capability, id, mapping.NameColumn is null ? null : row.GetValueOrDefault(mapping.NameColumn)));
        }
        return result;
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
}
