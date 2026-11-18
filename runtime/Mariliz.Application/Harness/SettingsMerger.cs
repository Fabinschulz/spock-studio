using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

// Todo o resto do arquivo é preservado como está. Formato conforme a documentação oficial do Claude Code (hooks e permissions).
public static class SettingsMerger
{
    public const string HookMatcher = "Bash|Write|Edit|MultiEdit|NotebookEdit";
    public static string HookScriptReference(HarnessNaming naming) => "${CLAUDE_PROJECT_DIR}/" + naming.HookScript;

    public static readonly IReadOnlyList<string> DenyRules =
        ["Read(./.env)", "Read(./.env.local)", "Read(./.env.production)", "Read(./.env.development)"];

    internal static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        // Sem isto, no Windows o arquivo sairia com \r\n por dentro e o \n final acrescentado abaixo.
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject DesiredHook(string pythonCommand, HarnessNaming naming) => new()
    {
        ["type"] = "command",
        ["command"] = pythonCommand,
        ["args"] = new JsonArray(HookScriptReference(naming)),
        ["timeout"] = 10
    };

    public static (string? Content, string? Error) Merge(string? current, string pythonCommand, HarnessNaming naming)
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

        var desired = DesiredHook(pythonCommand, naming);
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

    // C2 e C3: os hooks da IDE chamam o lançador spock (DD-6). O Codex usa o mesmo formato no .codex/hooks.json. O de sessão escreve o contexto de abertura; o de prompt, um aviso curto
    // só quando algo mudou. Sem matcher: valem para início, retomada, limpeza e compactação, e para todo prompt.
    public const string SessionCommand = "spock context session";
    public const string PromptCommand = "spock context prompt";

    private const string SessionEvent = "SessionStart";
    private const string PromptEvent = "UserPromptSubmit";

    public static (string? Content, string? Error) MergeSession(string? current, string path = Catalog.PathSettings) =>
        MergeEvent(current, path, SessionEvent, SessionCommand, 15);

    // Roda a cada prompt: o prazo curto impede que um motor travado segure a pergunta da pessoa.
    public static (string? Content, string? Error) MergePrompt(string? current, string path = Catalog.PathSettings) =>
        MergeEvent(current, path, PromptEvent, PromptCommand, 5);

    public static string? ReleaseSession(string? current) => ReleaseEvent(current, SessionEvent);

    public static string? ReleasePrompt(string? current) => ReleaseEvent(current, PromptEvent);

    public static bool HasSession(string? content) => HasEvent(content, SessionEvent);

    public static bool HasPrompt(string? content) => HasEvent(content, PromptEvent);

    private static (string? Content, string? Error) MergeEvent(string? current, string path, string hookEvent, string command, int timeout)
    {
        if (Parse(current) is not { } root) return (null, $"{path} não é um JSON válido (sem comentários nem vírgulas sobrando); a aplicação não o altera.");
        var before = root.DeepClone();
        if (!TryGetOrCreateObject(root, "hooks", out var hooks) || !TryGetOrCreateArray(hooks!, hookEvent, out var groups))
            return (null, $"{path} tem 'hooks' em formato inesperado; a aplicação não o altera.");

        var desired = new JsonObject { ["type"] = "command", ["command"] = command, ["timeout"] = timeout };
        var replaced = false;
        foreach (var group in groups!.OfType<JsonObject>())
        {
            if (group["hooks"] is not JsonArray list) continue;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject hook || !IsSpockContext(hook)) continue;
                list[i] = desired.DeepClone();
                replaced = true;
            }
        }
        if (!replaced) groups!.Add(new JsonObject { ["hooks"] = new JsonArray(desired) });

        if (!string.IsNullOrWhiteSpace(current) && JsonNode.DeepEquals(before, root)) return (current, null);
        return (root.ToJsonString(Output) + "\n", null);
    }

    // Nulo quando não havia o que tirar.
    private static string? ReleaseEvent(string? current, string hookEvent)
    {
        if (string.IsNullOrWhiteSpace(current) || Parse(current) is not { } root) return null;
        if (root["hooks"] is not JsonObject hooks || hooks[hookEvent] is not JsonArray groups) return null;

        var removed = false;
        foreach (var group in groups.OfType<JsonObject>().ToList())
        {
            if (group["hooks"] is not JsonArray list) continue;
            foreach (var hook in list.OfType<JsonObject>().Where(IsSpockContext).ToList())
            {
                list.Remove(hook);
                removed = true;
            }
            if (list.Count == 0) groups.Remove(group);
        }
        if (!removed) return null;

        if (groups.Count == 0) hooks.Remove(hookEvent);
        if (hooks.Count == 0) root.Remove("hooks");
        return root.ToJsonString(Output) + "\n";
    }

    private static bool HasEvent(string? content, string hookEvent) =>
        Parse(content) is { } root && root["hooks"]?[hookEvent] is JsonArray groups
        && groups.OfType<JsonObject>().Any(group => group["hooks"] is JsonArray list && list.OfType<JsonObject>().Any(IsSpockContext));

    // Só o que chama "spock context" é do Spock: um hook da equipe no mesmo evento fica onde está.
    private static bool IsSpockContext(JsonObject hook) =>
        hook["command"] is JsonValue value && value.TryGetValue<string>(out var command) && command.StartsWith("spock context ", StringComparison.Ordinal);

    internal static JsonObject? Parse(string? current)
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

    // Nos dois nomes: o hook de um projeto antigo continua sendo reconhecido e nunca ganha uma cópia ao lado.
    public static bool IsOurs(JsonObject hook)
    {
        if (hook["args"] is JsonArray args && args.Count > 0 && args[0] is JsonValue v && v.TryGetValue<string>(out var s))
            return HarnessNaming.All.Any(naming => s.EndsWith(naming.HookScript, StringComparison.Ordinal));
        return false;
    }

    internal static bool TryGetOrCreateObject(JsonObject parent, string name, out JsonObject? child)
    {
        if (parent[name] is null) { child = new JsonObject(); parent[name] = child; return true; }
        child = parent[name] as JsonObject;
        return child is not null;
    }

    internal static bool TryGetOrCreateArray(JsonObject parent, string name, out JsonArray? child)
    {
        if (parent[name] is null) { child = new JsonArray(); parent[name] = child; return true; }
        child = parent[name] as JsonArray;
        return child is not null;
    }
}
