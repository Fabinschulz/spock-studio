using System.Text.Json.Nodes;

namespace Mariliz.Application.Harness;

// C3: o .cursor/hooks.json tem formato próprio, {"version": 1, "hooks": {"sessionStart": [{"command", "timeout"}]}}, sem o grupo
// com matcher do Claude Code e do Codex. O Cursor não tem hook por prompt que injete contexto, então só há o de sessão.
public static class CursorHooksMerger
{
    // "--ide cursor": o Cursor lê o contexto de um JSON com additional_context, não do texto puro.
    public const string SessionCommand = "spock context session --ide cursor";
    private const string SessionEvent = "sessionStart";

    public static (string? Content, string? Error) MergeSession(string? current)
    {
        if (SettingsMerger.Parse(current) is not { } root)
            return (null, $"{Catalog.PathCursorHooks} não é um JSON válido (sem comentários nem vírgulas sobrando); a aplicação não o altera.");
        var before = root.DeepClone();
        root["version"] ??= 1;
        if (!SettingsMerger.TryGetOrCreateObject(root, "hooks", out var hooks) || !SettingsMerger.TryGetOrCreateArray(hooks!, SessionEvent, out var list))
            return (null, $"{Catalog.PathCursorHooks} tem 'hooks' em formato inesperado; a aplicação não o altera.");

        var desired = new JsonObject { ["command"] = SessionCommand, ["timeout"] = 15 };
        var replaced = false;
        for (var i = 0; i < list!.Count; i++)
        {
            if (list[i] is not JsonObject hook || !IsOurs(hook)) continue;
            list[i] = desired.DeepClone();
            replaced = true;
        }
        if (!replaced) list.Add(desired);

        if (!string.IsNullOrWhiteSpace(current) && JsonNode.DeepEquals(before, root)) return (current, null);
        return (root.ToJsonString(SettingsMerger.Output) + "\n", null);
    }

    // Nulo quando não havia o que tirar. O "version" fica: o arquivo continua sendo um hooks.json válido para o que a equipe pôs.
    public static string? ReleaseSession(string? current)
    {
        if (string.IsNullOrWhiteSpace(current) || SettingsMerger.Parse(current) is not { } root) return null;
        if (root["hooks"] is not JsonObject hooks || hooks[SessionEvent] is not JsonArray list) return null;

        var ours = list.OfType<JsonObject>().Where(IsOurs).ToList();
        if (ours.Count == 0) return null;
        foreach (var hook in ours) list.Remove(hook);
        if (list.Count == 0) hooks.Remove(SessionEvent);
        return root.ToJsonString(SettingsMerger.Output) + "\n";
    }

    public static bool HasSession(string? content) =>
        SettingsMerger.Parse(content) is { } root && root["hooks"]?[SessionEvent] is JsonArray list && list.OfType<JsonObject>().Any(IsOurs);

    private static bool IsOurs(JsonObject hook) =>
        hook["command"] is JsonValue value && value.TryGetValue<string>(out var command) && command.StartsWith("spock context ", StringComparison.Ordinal);
}
