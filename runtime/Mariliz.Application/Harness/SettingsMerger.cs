using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mariliz.Application.Harness;

// Todo o resto do arquivo é preservado como está. Formato conforme a documentação oficial do Claude Code (hooks e permissions).
public static class SettingsMerger
{
    public const string HookMatcher = "Bash|Write|Edit|MultiEdit|NotebookEdit";
    public const string HookScriptReference = "${CLAUDE_PROJECT_DIR}/" + Catalog.PathHookScript;

    public static readonly IReadOnlyList<string> DenyRules =
        ["Read(./.env)", "Read(./.env.local)", "Read(./.env.production)", "Read(./.env.development)"];

    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        // Sem isto, no Windows o arquivo sairia com \r\n por dentro e o \n final acrescentado abaixo.
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject DesiredHook(string pythonCommand) => new()
    {
        ["type"] = "command",
        ["command"] = pythonCommand,
        ["args"] = new JsonArray(HookScriptReference),
        ["timeout"] = 10
    };

    public static (string? Content, string? Error) Merge(string? current, string pythonCommand)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(current))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(current, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }) as JsonObject
                       ?? throw new JsonException();
            }
            catch (JsonException)
            {
                return (null, ".claude/settings.json não é um JSON válido (sem comentários nem vírgulas sobrando); a aplicação não o altera.");
            }
        }

        var before = root.DeepClone();

        if (!TryGetOrCreateObject(root, "hooks", out var hooks) || !TryGetOrCreateArray(hooks!, "PreToolUse", out var preToolUse))
            return (null, ".claude/settings.json tem 'hooks' em formato inesperado; a aplicação não o altera.");

        var desired = DesiredHook(pythonCommand);
        var replaced = false;
        foreach (var group in preToolUse!.OfType<JsonObject>())
        {
            if (group["hooks"] is not JsonArray list) continue;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject hook || !IsOurs(hook)) continue;
                list[i] = desired.DeepClone();
                if (list.Count == 1) group["matcher"] = HookMatcher;
                replaced = true;
            }
        }
        if (!replaced)
            preToolUse!.Add(new JsonObject { ["matcher"] = HookMatcher, ["hooks"] = new JsonArray(desired.DeepClone()) });

        if (!TryGetOrCreateObject(root, "permissions", out var permissions) || !TryGetOrCreateArray(permissions!, "deny", out var deny))
            return (null, ".claude/settings.json tem 'permissions' em formato inesperado; a aplicação não o altera.");
        foreach (var rule in DenyRules)
            if (!deny!.Any(n => n?.GetValue<string>() == rule)) deny!.Add(rule);

        // Sem mudança semântica, devolve o texto original para não reformatar o arquivo da pessoa.
        if (!string.IsNullOrWhiteSpace(current) && JsonNode.DeepEquals(before, root)) return (current, null);
        return (root.ToJsonString(Output) + "\n", null);
    }

    // As regras que negam a leitura de .env ficam (tirá-las abriria o acesso a segredos), e o arquivo nunca é apagado.
    public static string? Release(string? current)
    {
        if (string.IsNullOrWhiteSpace(current)) return null;
        JsonObject root;
        try
        {
            root = JsonNode.Parse(current, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }) as JsonObject
                   ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return null;
        }

        if (root["hooks"] is not JsonObject hooks || hooks["PreToolUse"] is not JsonArray preToolUse) return null;

        var removed = false;
        foreach (var group in preToolUse.OfType<JsonObject>().ToList())
        {
            if (group["hooks"] is not JsonArray list) continue;
            foreach (var hook in list.OfType<JsonObject>().Where(IsOurs).ToList())
            {
                list.Remove(hook);
                removed = true;
            }
            if (list.Count == 0) preToolUse.Remove(group);
        }
        if (!removed) return null;

        if (preToolUse.Count == 0) hooks.Remove("PreToolUse");
        if (hooks.Count == 0) root.Remove("hooks");
        return root.ToJsonString(Output) + "\n";
    }

    // C2: o hook de início de sessão chama o lançador spock (DD-6), que escreve o contexto de abertura na saída. Sem matcher:
    // vale para início, retomada, limpeza e compactação.
    public const string SessionCommand = "spock context session";

    public static (string? Content, string? Error) MergeSession(string? current)
    {
        if (Parse(current) is not { } root) return (null, ".claude/settings.json não é um JSON válido (sem comentários nem vírgulas sobrando); a aplicação não o altera.");
        var before = root.DeepClone();
        if (!TryGetOrCreateObject(root, "hooks", out var hooks) || !TryGetOrCreateArray(hooks!, "SessionStart", out var sessionStart))
            return (null, ".claude/settings.json tem 'hooks' em formato inesperado; a aplicação não o altera.");

        var desired = new JsonObject { ["type"] = "command", ["command"] = SessionCommand, ["timeout"] = 15 };
        var replaced = false;
        foreach (var group in sessionStart!.OfType<JsonObject>())
        {
            if (group["hooks"] is not JsonArray list) continue;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject hook || !IsOurSession(hook)) continue;
                list[i] = desired.DeepClone();
                replaced = true;
            }
        }
        if (!replaced) sessionStart!.Add(new JsonObject { ["hooks"] = new JsonArray(desired) });

        if (!string.IsNullOrWhiteSpace(current) && JsonNode.DeepEquals(before, root)) return (current, null);
        return (root.ToJsonString(Output) + "\n", null);
    }

    // Nulo quando não havia o que tirar.
    public static string? ReleaseSession(string? current)
    {
        if (string.IsNullOrWhiteSpace(current) || Parse(current) is not { } root) return null;
        if (root["hooks"] is not JsonObject hooks || hooks["SessionStart"] is not JsonArray sessionStart) return null;

        var removed = false;
        foreach (var group in sessionStart.OfType<JsonObject>().ToList())
        {
            if (group["hooks"] is not JsonArray list) continue;
            foreach (var hook in list.OfType<JsonObject>().Where(IsOurSession).ToList())
            {
                list.Remove(hook);
                removed = true;
            }
            if (list.Count == 0) sessionStart.Remove(group);
        }
        if (!removed) return null;

        if (sessionStart.Count == 0) hooks.Remove("SessionStart");
        if (hooks.Count == 0) root.Remove("hooks");
        return root.ToJsonString(Output) + "\n";
    }

    public static bool HasSession(string? content) =>
        Parse(content) is { } root && root["hooks"]?["SessionStart"] is JsonArray groups
        && groups.OfType<JsonObject>().Any(group => group["hooks"] is JsonArray list && list.OfType<JsonObject>().Any(IsOurSession));

    private static bool IsOurSession(JsonObject hook) =>
        hook["command"] is JsonValue value && value.TryGetValue<string>(out var command) && command.StartsWith("spock context ", StringComparison.Ordinal);

    private static JsonObject? Parse(string? current)
    {
        if (string.IsNullOrWhiteSpace(current)) return new JsonObject();
        try
        {
            return JsonNode.Parse(current, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false }) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool IsOurs(JsonObject hook)
    {
        if (hook["args"] is JsonArray args && args.Count > 0 && args[0] is JsonValue v && v.TryGetValue<string>(out var s))
            return s.EndsWith(Catalog.PathHookScript, StringComparison.Ordinal);
        return false;
    }

    private static bool TryGetOrCreateObject(JsonObject parent, string name, out JsonObject? child)
    {
        if (parent[name] is null) { child = new JsonObject(); parent[name] = child; return true; }
        child = parent[name] as JsonObject;
        return child is not null;
    }

    private static bool TryGetOrCreateArray(JsonObject parent, string name, out JsonArray? child)
    {
        if (parent[name] is null) { child = new JsonArray(); parent[name] = child; return true; }
        child = parent[name] as JsonArray;
        return child is not null;
    }
}
