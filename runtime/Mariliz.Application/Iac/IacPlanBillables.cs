using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Iac;

// Do `show -json` de um plano, os recursos cobrados que mudam e só os atributos que definem o preço. Nome, senha, tag e
// qualquer outro valor ficam no arquivo de plano. Os tipos são os que a estimativa sabe cotar com a lista pública da AWS.
public static partial class IacPlanBillables
{
    public const string Unknown = "?";

    private static readonly Dictionary<string, string[]> PriceAttributes = new(StringComparer.Ordinal)
    {
        ["aws_instance"] = ["instance_type"],
        ["aws_nat_gateway"] = ["connectivity_type"],
        ["aws_lb"] = ["load_balancer_type"],
        ["aws_db_instance"] = ["instance_class", "engine", "multi_az", "allocated_storage", "storage_type"],
        ["aws_ecs_service"] = ["desired_count", "launch_type"],
        ["aws_secretsmanager_secret"] = [],
        ["aws_cloudwatch_metric_alarm"] = []
    };

    public static bool Priced(string type) => PriceAttributes.ContainsKey(type);

    public static IReadOnlyList<IacBillable> Read(JsonNode plan)
    {
        var changes = (plan["resource_changes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var taskDefinitions = TaskDefinitionReferences(plan["configuration"]?["root_module"] as JsonObject, "");
        var billables = new List<IacBillable>();
        foreach (var resource in changes)
        {
            var type = Text(resource["type"]) ?? "";
            if (Text(resource["mode"]) == "data" || !PriceAttributes.TryGetValue(type, out var names)) continue;
            var change = resource["change"] as JsonObject;
            var action = Action((change?["actions"] as JsonArray ?? []).Select(a => a?.GetValue<string>()).OfType<string>().ToList());
            if (action is null) continue;

            var address = Text(resource["address"]) ?? "?";
            var before = action == "create" ? null : Values(change?["before"] as JsonObject, null, names);
            var after = action == "delete" ? null : Values(change?["after"] as JsonObject, change?["after_unknown"] as JsonObject, names);
            if (type == "aws_ecs_service")
            {
                // CPU e memória são da task definition referenciada, no mesmo módulo.
                var task = taskDefinitions.GetValueOrDefault(WithoutIndexes(address)) is { } reference
                    ? changes.FirstOrDefault(c => WithoutIndexes(Text(c["address"]) ?? "") == reference)?["change"] as JsonObject
                    : null;
                before = Merge(before, Values(task?["before"] as JsonObject, null, ["cpu", "memory"]));
                after = Merge(after, Values(task?["after"] as JsonObject, task?["after_unknown"] as JsonObject, ["cpu", "memory"]));
            }
            if (action == "update" && Same(before, after)) continue;
            billables.Add(new IacBillable(address, type, action, before, after));
        }
        return billables;
    }

    private static string? Action(IReadOnlyList<string> actions) => actions switch
    {
        ["create"] => "create",
        ["update"] => "update",
        ["delete"] => "delete",
        ["delete", "create"] or ["create", "delete"] => "replace",
        _ => null
    };

    private static Dictionary<string, string>? Values(JsonObject? values, JsonObject? unknown, IReadOnlyList<string> names)
    {
        if (values is null && unknown is null) return null;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (unknown?[name] is JsonValue flag && flag.TryGetValue<bool>(out var isUnknown) && isUnknown) result[name] = Unknown;
            else if (Scalar(values?[name]) is { } value) result[name] = value;
        }
        return result;
    }

    private static Dictionary<string, string>? Merge(Dictionary<string, string>? target, Dictionary<string, string>? extra)
    {
        if (target is null) return null;
        foreach (var (name, value) in extra ?? []) target[name] = value;
        return target;
    }

    private static bool Same(IReadOnlyDictionary<string, string>? a, IReadOnlyDictionary<string, string>? b) =>
        a is not null && b is not null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    // Endereço do serviço ECS na configuração → endereço da task definition que ele referencia, com o prefixo do módulo.
    private static Dictionary<string, string> TaskDefinitionReferences(JsonObject? module, string prefix)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (module is null) return map;
        foreach (var resource in (module["resources"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(resource["type"]) != "aws_ecs_service") continue;
            var target = (resource["expressions"]?["task_definition"]?["references"] as JsonArray ?? [])
                .Select(r => Text(r)).OfType<string>()
                .FirstOrDefault(r => r.StartsWith("aws_ecs_task_definition.", StringComparison.Ordinal) && r.Count(c => c == '.') == 1);
            if (target is not null) map[prefix + Text(resource["address"])] = prefix + target;
        }
        foreach (var (name, call) in module["module_calls"] as JsonObject ?? [])
            foreach (var (key, value) in TaskDefinitionReferences(call?["module"] as JsonObject, $"{prefix}module.{name}."))
                map[key] = value;
        return map;
    }

    private static string WithoutIndexes(string address) => IndexPattern().Replace(address, "");

    private static string? Scalar(JsonNode? node) => node switch
    {
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        JsonValue v when v.GetValueKind() == JsonValueKind.Number => v.GetValue<decimal>().ToString(CultureInfo.InvariantCulture),
        JsonValue v when v.GetValueKind() is JsonValueKind.True or JsonValueKind.False => v.GetValue<bool>() ? "true" : "false",
        _ => null
    };

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex IndexPattern();
}
