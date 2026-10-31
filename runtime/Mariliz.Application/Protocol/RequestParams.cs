using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

internal readonly struct RequestParams(JsonObject? values)
{
    public string Str(string name) =>
        OptionalStr(name) ?? throw new ParamException($"'{name}' é obrigatório.");

    public string? OptionalStr(string name) =>
        values?[name] is JsonValue v && v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    public bool Flag(string name) => values?[name]?.GetValue<bool>() ?? false;

    public JsonObject? Object(string name) => values?[name] as JsonObject;

    public JsonObject ObjectOrEmpty(string name)
    {
        if (values?[name] is null) return new JsonObject();
        return values[name] as JsonObject ?? throw new ParamException($"'{name}' precisa ser um objeto.");
    }

    public List<string> StrList(string name)
    {
        if (values?[name] is null) return [];
        if (values[name] is not JsonArray array) throw new ParamException($"'{name}' precisa ser uma lista.");

        return array
            .Select(n => n is JsonValue v && v.TryGetValue<string>(out var text) ? text : throw new ParamException($"'{name}' só aceita textos."))
            .ToList();
    }
}
