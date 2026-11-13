using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

public static class JsonFields
{
    public static string Text(this JsonObject item, string name) =>
        item[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
