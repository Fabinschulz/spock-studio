using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Mariliz.Application.Integrations;

/// <summary>O mesmo formato no disco e no protocolo: camelCase, enums como texto, datas ISO 8601.</summary>
public static class IntegrationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static JsonNode? Node<T>(T value) => JsonSerializer.SerializeToNode(value, Options);
}
