using System.Globalization;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Boot;

/// <summary>
/// Leitura tolerante do JSON que os tratadores devolvem: campo ausente vira nulo, nunca zero inventado. Os resumos do Boot
/// só usam o que sai daqui para contar e nomear; o texto livre do projeto e das integrações não passa por estas funções.
/// </summary>
internal static class SummaryJson
{
    public const string NoValue = "sem dado";

    public static List<JsonObject> Array(JsonObject? node, string name) =>
        node?[name] is JsonArray array ? [.. array.OfType<JsonObject>()] : [];

    // Para listas de texto, que Array (só objetos) não enxerga.
    public static int Size(JsonObject? node, string name) => node?[name] is JsonArray array ? array.Count : 0;

    public static bool HasList(JsonObject? node, string name) => node?[name] is JsonArray;

    public static JsonObject? Object(JsonObject? node, string name) => node?[name] as JsonObject;

    public static string? Text(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    public static int? Number(JsonObject? node, string name)
    {
        if (node?[name] is not JsonValue value) return null;
        if (value.TryGetValue<int>(out var whole)) return whole;
        if (value.TryGetValue<long>(out var big) && big is >= int.MinValue and <= int.MaxValue) return (int)big;
        return value.TryGetValue<double>(out var real) && real == Math.Floor(real) && Math.Abs(real) < int.MaxValue ? (int)real : null;
    }

    public static double? Decimal(JsonObject? node, string name)
    {
        if (node?[name] is not JsonValue value) return null;
        if (value.TryGetValue<double>(out var real)) return real;
        if (value.TryGetValue<long>(out var big)) return big;
        return value.TryGetValue<int>(out var whole) ? whole : null;
    }

    public static bool? Flag(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    // Só a data: o instante exato não ajuda a ler o resumo.
    public static string? Day(JsonObject? node, string name) =>
        Text(node, name) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    public static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";

    public static string Count(int? count) => count?.ToString(CultureInfo.InvariantCulture) ?? NoValue;

    public static string Days(double? days) => days switch
    {
        null => NoValue,
        1 => "1 dia",
        { } value => $"{Fixed(value)} dias"
    };

    public static string Fixed(double? value) =>
        value is null ? NoValue : value.Value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    public static string Label(string? key, IReadOnlyDictionary<string, string> labels) =>
        key is not null && labels.TryGetValue(key, out var label) ? label : key ?? NoValue;

    public static CapabilitySummary NoData(string what) => new($"Sem dado {what}.", []);
}
