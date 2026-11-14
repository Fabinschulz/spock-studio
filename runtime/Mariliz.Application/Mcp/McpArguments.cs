using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Mcp;

internal static class McpArguments
{
    public static string? Text(JsonObject args, string name, int maxLength)
    {
        var node = args[name];
        if (node is null) return null;
        if (node.GetValueKind() != JsonValueKind.String) throw new McpToolException($"'{name}' precisa ser um texto.");
        var text = node.GetValue<string>().Trim();
        if (text.Length > maxLength) throw new McpToolException($"'{name}' passa de {maxLength} caracteres.");
        return text.Length == 0 ? null : text;
    }

    public static int Number(JsonObject args, string name, int fallback, int min, int max)
    {
        var node = args[name];
        if (node is null) return fallback;
        if (node.GetValueKind() != JsonValueKind.Number || !node.AsValue().TryGetValue<int>(out var value))
            throw new McpToolException($"'{name}' precisa ser um número inteiro.");
        return value >= min && value <= max ? value : throw new McpToolException($"'{name}' vai de {min} a {max}.");
    }
}
