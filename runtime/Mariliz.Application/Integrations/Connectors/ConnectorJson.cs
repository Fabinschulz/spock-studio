using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Integrations;

// O que vem de fora nunca derruba a sincronização com exceção de tipo.
internal static class ConnectorJson
{
    public static JsonNode Parse(ConnectorResponse response)
    {
        switch (response.Status)
        {
            case 401: throw IntegrationErrors.AuthFailed();
            case 403: throw IntegrationErrors.Forbidden();
            case 404 or 410: throw IntegrationErrors.ResourceNotFound();
            case < 200 or >= 300: throw IntegrationErrors.InvalidResponse();
        }

        try
        {
            return JsonNode.Parse(response.Body) ?? throw IntegrationErrors.InvalidResponse();
        }
        catch (JsonException)
        {
            throw IntegrationErrors.InvalidResponse();
        }
    }

    public static string? Text(JsonNode? node) =>
        JsonRead.NonBlank(node);

    public static string RequiredText(JsonNode? node) => Text(node) ?? throw IntegrationErrors.InvalidResponse();

    public static bool Flag(JsonNode? node) => JsonRead.Flag(node);

    public static IEnumerable<JsonObject> Objects(JsonNode? node) =>
        node is JsonArray array ? array.OfType<JsonObject>() : [];

    public static DateTimeOffset? Date(JsonNode? node)
    {
        if (Text(node) is not { } text) return null;
        // O Jira escreve o fuso sem dois-pontos ("-0300"); o parser do .NET só aceita com ("-03:00").
        if (text.Length > 5 && text[^5] is '+' or '-' && text[^3] != ':') text = text[..^2] + ":" + text[^2..];
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToUniversalTime()
            : null;
    }

    public static DateTimeOffset RequiredDate(JsonNode? node) => Date(node) ?? throw IntegrationErrors.InvalidResponse();

    public static DateOnly? Day(JsonNode? node) =>
        Text(node) is { } text && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : null;
}
