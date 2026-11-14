using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Mariliz.Application.Ports;

namespace Mariliz.Application.Security;

// O que deixa uma ferramenta de IA agir no projeto sem pedir confirmação.
internal static class AiConfigRules
{
    public const string BypassPermissions = "bypass-permissions";
    public const string BroadBash = "broad-bash";
    public const string CodexFullAccess = "codex-full-access";
    public const string CodexNoApproval = "codex-no-approval";

    public static readonly IReadOnlyList<string> ClaudeSettings = [".claude/settings.json", ".claude/settings.local.json"];
    private const string CodexConfig = ".codex/config.toml";

    private static readonly HashSet<string> UnrestrictedBash = new(StringComparer.Ordinal) { "Bash", "Bash(*)", "Bash(:*)" };
    private static readonly Regex FullAccess = new(@"^\s*sandbox_mode\s*=\s*""danger-full-access""", RegexOptions.Multiline);
    private static readonly Regex NoApproval = new(@"^\s*approval_policy\s*=\s*""never""", RegexOptions.Multiline);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static IEnumerable<(string Path, string Rule)> Inspect(IProjectFiles files)
    {
        foreach (var path in ClaudeSettings)
        {
            if (Parse(files.ReadText(path)) is not { } settings) continue;
            if (IsBypass(settings)) yield return (path, BypassPermissions);
            if (BroadAllows(settings).Any()) yield return (path, BroadBash);
        }

        var codex = files.ReadText(CodexConfig);
        if (codex is null) yield break;
        if (FullAccess.IsMatch(codex)) yield return (CodexConfig, CodexFullAccess);
        if (NoApproval.IsMatch(codex)) yield return (CodexConfig, CodexNoApproval);
    }

    public static bool IsFixable(string rule) => rule is BypassPermissions or BroadBash;

    // Tira o modo que pula confirmações e as permissões de Bash sem restrição; o resto do arquivo fica como estava.
    public static string? FixClaudeSettings(string? text)
    {
        if (Parse(text) is not { } settings || settings["permissions"] is not JsonObject permissions) return null;
        var changed = false;
        if (IsBypass(settings))
        {
            permissions.Remove("defaultMode");
            changed = true;
        }
        if (permissions["allow"] is JsonArray allow)
        {
            foreach (var entry in BroadAllows(settings).ToList())
            {
                allow.Remove(entry);
                changed = true;
            }
        }
        return changed ? settings.ToJsonString(Indented) + "\n" : null;
    }

    private static bool IsBypass(JsonObject settings) =>
        settings["permissions"]?["defaultMode"] is JsonValue mode && mode.TryGetValue<string>(out var value) && value == "bypassPermissions";

    private static IEnumerable<JsonNode> BroadAllows(JsonObject settings) =>
        settings["permissions"]?["allow"] is JsonArray allow
            ? allow.OfType<JsonValue>().Where(entry => entry.TryGetValue<string>(out var rule) && UnrestrictedBash.Contains(rule.Trim()))
            : [];

    private static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
