using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

public static class JournalJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
}
