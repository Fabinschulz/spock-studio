using System.Text.Json.Nodes;

namespace Mariliz.Application.Protocol;

/// <summary>Leitura tolerante de campos de mensagens que vêm de fora (interface, runtime de IA).</summary>
public static class JsonFields
{
    /// <summary>O texto do campo, ou vazio se ele faltar ou não for texto.</summary>
    public static string Text(this JsonObject item, string name) =>
        item[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
}
