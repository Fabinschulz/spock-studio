using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

internal readonly struct RequestParams(JsonObject? values)
{
    public string Str(string name) =>
        OptionalStr(name) ?? throw new ParamException($"'{name}' é obrigatório.");

    public string? OptionalStr(string name) =>
        values?[name] is JsonValue v && v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    public bool Flag(string name)
    {
        if (values?[name] is null) return false;
        return values[name] is JsonValue v && v.TryGetValue<bool>(out var flag)
            ? flag
            : throw new ParamException($"'{name}' precisa ser verdadeiro ou falso.");
    }

    public int? OptionalInt(string name)
    {
        if (values?[name] is null) return null;
        return values[name] is JsonValue v && v.TryGetValue<int>(out var number) && number >= -1
            ? number
            : throw new ParamException($"'{name}' precisa ser um inteiro a partir de -1.");
    }

    /// <summary>Uma lista de textos opcional. Qualquer item que não seja texto invalida o pedido.</summary>
    public IReadOnlyList<string> StringList(string name)
    {
        if (values?[name] is null) return [];
        if (values[name] is not JsonArray items) throw new ParamException($"'{name}' precisa ser uma lista de textos.");
        var list = new List<string>();
        foreach (var item in items)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new ParamException($"'{name}' precisa ser uma lista de textos.");
            list.Add(text);
        }
        return list;
    }

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
