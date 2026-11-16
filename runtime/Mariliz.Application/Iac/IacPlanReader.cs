using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Iac;

// Lê a saída de `show -json` de um plano (mesmo formato no OpenTofu e no Terraform). Do plano saem só contagens,
// endereços, tipos e ações, e achados de segurança cuja mensagem cita o endereço e a regra, nunca o valor.
public static class IacPlanReader
{
    public static (IacPlanSummary Summary, IReadOnlyList<IacFinding> Findings) Read(JsonNode plan)
    {
        var changes = new List<IacResourceChange>();
        var findings = new List<IacFinding>();
        int create = 0, update = 0, replace = 0, delete = 0, read = 0, unchanged = 0;

        foreach (var resource in (plan["resource_changes"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var address = Text(resource, "address") ?? "?";
            var type = Text(resource, "type") ?? "?";
            var change = resource["change"] as JsonObject;
            var actions = (change?["actions"] as JsonArray ?? []).Select(a => a?.GetValue<string>()).OfType<string>().ToList();
            var action = Action(actions);
            switch (action)
            {
                case "create": create++; break;
                case "update": update++; break;
                case "replace": replace++; break;
                case "delete": delete++; break;
                case "read": read++; break;
                default: unchanged++; break;
            }
            if (action == "no-op") continue;
            changes.Add(new IacResourceChange(address, type, action));

            if (action == "delete")
                findings.Add(new IacFinding(IacFindingSeverity.Danger, "destroy", address, $"{address} será destruído."));
            else if (action == "replace")
                findings.Add(new IacFinding(IacFindingSeverity.Danger, "replace", address, $"{address} será destruído e criado de novo."));
            if (action is "create" or "update" or "replace" && change?["after"] is JsonObject after)
                findings.AddRange(Check(type, address, after));
        }

        var outputs = (plan["output_changes"] as JsonObject ?? [])
            .Where(o => o.Value?["actions"] is JsonArray a && Action(a.Select(x => x?.GetValue<string>()).OfType<string>().ToList()) != "no-op")
            .Select(o => o.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        return (new IacPlanSummary(create, update, replace, delete, read, unchanged, changes, outputs), findings);
    }

    public const int MaxDriftAttributes = 20;

    // resource_drift de um plano: o que a nuvem tem diferente do state. Dos atributos saem só os nomes de nível de cima cujo
    // valor mudou; a comparação acontece aqui e os valores ficam no arquivo de plano, que é apagado em seguida.
    public static IReadOnlyList<IacDriftedResource> ReadDrift(JsonNode plan)
    {
        var drifted = new List<IacDriftedResource>();
        foreach (var resource in (plan["resource_drift"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (Text(resource, "mode") == "data") continue;
            var change = resource["change"] as JsonObject;
            var actions = (change?["actions"] as JsonArray ?? []).Select(a => a?.GetValue<string>()).OfType<string>().ToList();
            var kind = Action(actions) switch
            {
                "delete" => "deleted",
                "update" or "replace" => "changed",
                _ => null
            };
            if (kind is null) continue;
            var attributes = kind == "deleted" ? [] : ChangedAttributes(change?["before"] as JsonObject, change?["after"] as JsonObject);
            drifted.Add(new IacDriftedResource(Text(resource, "address") ?? "?", Text(resource, "type") ?? "?", kind, attributes));
        }
        return drifted;
    }

    private static List<string> ChangedAttributes(JsonObject? before, JsonObject? after)
    {
        if (before is null || after is null) return [];
        return before.Select(p => p.Key).Union(after.Select(p => p.Key), StringComparer.Ordinal)
            .Where(name => !JsonNode.DeepEquals(before[name], after[name]))
            .Order(StringComparer.Ordinal)
            .Take(MaxDriftAttributes)
            .ToList();
    }

    private static string Action(IReadOnlyList<string> actions) => actions switch
    {
        ["create"] => "create",
        ["update"] => "update",
        ["delete"] => "delete",
        ["read"] => "read",
        ["delete", "create"] or ["create", "delete"] => "replace",
        _ => "no-op"
    };

    private static IEnumerable<IacFinding> Check(string type, string address, JsonObject after)
    {
        switch (type)
        {
            case "aws_security_group":
                foreach (var rule in (after["ingress"] as JsonArray ?? []).OfType<JsonObject>())
                    if (OpenToWorld(rule, "cidr_blocks", "ipv6_cidr_blocks") && !WebOnly(rule, "from_port", "to_port"))
                        yield return Open(address, rule, "from_port", "to_port");
                break;
            case "aws_security_group_rule" when Text(after, "type") == "ingress":
                if (OpenToWorld(after, "cidr_blocks", "ipv6_cidr_blocks") && !WebOnly(after, "from_port", "to_port"))
                    yield return Open(address, after, "from_port", "to_port");
                break;
            case "aws_vpc_security_group_ingress_rule":
                if ((Text(after, "cidr_ipv4") == "0.0.0.0/0" || Text(after, "cidr_ipv6") == "::/0") && !WebOnly(after, "from_port", "to_port"))
                    yield return Open(address, after, "from_port", "to_port");
                break;
            case "aws_iam_policy" or "aws_iam_role_policy" or "aws_iam_user_policy" or "aws_iam_group_policy":
                if (WildcardPolicy(Text(after, "policy")))
                    yield return new IacFinding(IacFindingSeverity.Danger, "iam-wildcard", address, $"{address} concede todas as ações em todos os recursos (\"*\").");
                break;
            case "aws_ecr_repository" when Text(after, "image_tag_mutability") == "MUTABLE":
                yield return new IacFinding(IacFindingSeverity.Warning, "ecr-mutable-tag", address, $"{address} aceita sobrescrever tags de imagem; o modelo usa IMMUTABLE.");
                break;
            case "aws_db_instance" or "aws_rds_cluster_instance" when Flag(after, "publicly_accessible"):
                yield return new IacFinding(IacFindingSeverity.Danger, "db-public", address, $"{address} fica acessível pela internet.");
                break;
            case "aws_s3_bucket_public_access_block":
                if (new[] { "block_public_acls", "block_public_policy", "ignore_public_acls", "restrict_public_buckets" }.Any(f => after[f] is JsonValue && !Flag(after, f)))
                    yield return new IacFinding(IacFindingSeverity.Warning, "s3-public-access", address, $"{address} não bloqueia todo acesso público.");
                break;
            case "azurerm_network_security_rule":
                if (Text(after, "direction") == "Inbound" && Text(after, "access") == "Allow"
                    && Text(after, "source_address_prefix") is "*" or "0.0.0.0/0" or "Internet")
                    yield return new IacFinding(IacFindingSeverity.Danger, "open-ingress", address, $"{address} libera entrada de qualquer origem na internet.");
                break;
            case "google_compute_firewall":
                if (Text(after, "direction") is null or "INGRESS" && Strings(after, "source_ranges").Contains("0.0.0.0/0"))
                    yield return new IacFinding(IacFindingSeverity.Danger, "open-ingress", address, $"{address} libera entrada de qualquer origem na internet.");
                break;
            case "google_storage_bucket_iam_member" or "google_storage_bucket_iam_binding":
                if (Text(after, "member") is "allUsers" or "allAuthenticatedUsers" || Strings(after, "members").Any(m => m is "allUsers" or "allAuthenticatedUsers"))
                    yield return new IacFinding(IacFindingSeverity.Danger, "bucket-public", address, $"{address} torna o bucket público.");
                break;
        }
    }

    private static IacFinding Open(string address, JsonObject rule, string from, string to)
    {
        var low = Number(rule, from);
        var high = Number(rule, to);
        var ports = low is null ? "todas as portas" : low == high ? $"a porta {low}" : $"as portas {low} a {high}";
        return new IacFinding(IacFindingSeverity.Danger, "open-ingress", address, $"{address} abre {ports} para qualquer endereço da internet.");
    }

    private static bool OpenToWorld(JsonObject rule, string ipv4, string ipv6) =>
        Strings(rule, ipv4).Contains("0.0.0.0/0") || Strings(rule, ipv6).Contains("::/0");

    // 80 e 443 sozinhas são a exceção esperada de um load balancer público.
    private static bool WebOnly(JsonObject rule, string from, string to) =>
        Number(rule, from) is { } low && Number(rule, to) is { } high && low == high && low is 80 or 443;

    private static bool WildcardPolicy(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy)) return false;
        try
        {
            var statements = JsonNode.Parse(policy)?["Statement"] switch
            {
                JsonArray array => array.OfType<JsonObject>(),
                JsonObject single => [single],
                _ => []
            };
            return statements.Any(s => Text(s, "Effect") == "Allow" && Any(s["Action"], "*") && Any(s["Resource"], "*"));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Any(JsonNode? node, string value) => node switch
    {
        JsonValue v => v.TryGetValue<string>(out var text) && text == value,
        JsonArray a => a.Any(item => item is JsonValue v && v.TryGetValue<string>(out var text) && text == value),
        _ => false
    };

    private static string? Text(JsonObject node, string name) => node[name] is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static bool Flag(JsonObject node, string name) => node[name] is JsonValue v && v.TryGetValue<bool>(out var flag) && flag;

    private static long? Number(JsonObject node, string name) => node[name] is JsonValue v && v.TryGetValue<long>(out var number) ? number : null;

    private static List<string> Strings(JsonObject node, string name) =>
        (node[name] as JsonArray ?? []).Select(item => item is JsonValue v && v.TryGetValue<string>(out var text) ? text : null).OfType<string>().ToList();
}
