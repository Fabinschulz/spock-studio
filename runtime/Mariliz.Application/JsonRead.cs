using System.Text.Json.Nodes;

namespace Mariliz.Application;

// Leitura tolerante de JSON que veio de fora ou do disco. Campo ausente, nulo ou de outro tipo vira null:
// quem precisa de "" ou de um padrão escreve isso no ponto de uso, para a regra não divergir entre cópias.
public static class JsonRead
{
    public static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    public static string? Text(JsonObject? node, string name) => Text(node?[name]);

    // Para identificadores e versões, em que um texto só de espaços não diz nada.
    public static string? NonBlank(JsonNode? node) => Text(node) is { } text && !string.IsNullOrWhiteSpace(text) ? text : null;

    public static int? Int(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    public static int? Int(JsonObject? node, string name) => Int(node?[name]);

    public static bool Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    public static bool Flag(JsonObject? node, string name) => Flag(node?[name]);
}
