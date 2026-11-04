using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

public static class JournalJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    /// <summary>Qualquer objeto do motor no mesmo formato do diário: é assim que ele sai no protocolo e na CLI.</summary>
    public static JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, Options);
}
