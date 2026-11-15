using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Iac;

// Lê o desenho de um objeto JSON (da tela ou do modelo) sem confiar no formato: campo ausente, de tipo errado ou com
// espaços vira vazio e as checagens apontam o que falta. Nada aqui lança por causa do conteúdo.
public static class IacBlueprintJson
{
    public const int MaxText = 200;
    public const int MaxItems = 20;

    public static IacBlueprint Blueprint(JsonObject json)
    {
        var network = json["network"] as JsonObject;
        return new IacBlueprint(
            Text(json, "provider") ?? "aws",
            Text(json, "project") ?? "",
            Text(json, "region") ?? "",
            Text(json, "account"),
            Text(json, "profile"),
            new IacNetwork(network is null ? "" : Text(network, "vpcId") ?? "", Texts(network, "privateSubnets"), Texts(network, "publicSubnets")),
            json["service"] is JsonObject service ? Service(service) : null,
            json["database"] is JsonObject database ? Database(database) : null,
            Texts(json, "queues"),
            Text(json, "alarmEmail"),
            json["backend"] is JsonObject backend && Text(backend, "bucket") is { } bucket ? new IacBackendSpec(bucket, Text(backend, "region") ?? "") : null);
    }

    public static IacArchitectureNotes Notes(JsonObject? json)
    {
        if (json is null) return new IacArchitectureNotes([], [], [], []);
        var services = (json["services"] as JsonArray ?? []).OfType<JsonObject>().Take(MaxItems)
            .Select(item => new IacServiceReason(Text(item, "name") ?? "", Text(item, "reason", 400) ?? ""))
            .Where(reason => reason.Name.Length > 0 && reason.Reason.Length > 0).ToList();
        return new IacArchitectureNotes(services, Texts(json, "risks", 400), Texts(json, "alternatives", 400), Texts(json, "assumptions", 400));
    }

    public static JsonObject? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IacServiceSpec Service(JsonObject json) => new(
        Text(json, "name") ?? "",
        Number(json, "port") ?? 0,
        Text(json, "healthPath") ?? "/health",
        Number(json, "cpu") ?? 0,
        Number(json, "memory") ?? 0,
        Number(json, "desiredCount") ?? 1,
        Text(json, "certificateArn") ?? "",
        json["publicIp"] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag);

    private static IacDatabaseSpec Database(JsonObject json) => new(
        Text(json, "engine") ?? "",
        Text(json, "engineVersion") ?? "",
        Text(json, "instanceClass") ?? "",
        Number(json, "storageGb") ?? 0,
        json["multiAz"] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag);

    private static string? Text(JsonObject json, string name, int max = MaxText)
    {
        if (json[name] is not JsonValue value || !value.TryGetValue<string>(out var text)) return null;
        text = text.Trim();
        return text.Length == 0 ? null : text.Length > max ? text[..max] : text;
    }

    private static IReadOnlyList<string> Texts(JsonObject? json, string name, int max = MaxText) =>
        (json?[name] as JsonArray ?? []).OfType<JsonValue>()
        .Select(value => value.TryGetValue<string>(out var text) ? text.Trim() : "")
        .Where(text => text.Length > 0).Select(text => text.Length > max ? text[..max] : text).Take(MaxItems).ToList();

    private static int? Number(JsonObject json, string name) => json[name] switch
    {
        JsonValue value when value.TryGetValue<int>(out var number) => number,
        JsonValue value when value.TryGetValue<double>(out var real) && real is >= int.MinValue and <= int.MaxValue && real == Math.Floor(real) => (int)real,
        JsonValue value when value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };
}
