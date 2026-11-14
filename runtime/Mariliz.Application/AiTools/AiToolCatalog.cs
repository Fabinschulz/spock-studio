using Mariliz.Application.Discovery;

namespace Mariliz.Application.AiTools;

public static class AiToolCatalog
{
    public const string Gemini = "gemini";

    public static readonly IReadOnlyList<AiToolDefinition> All =
    [
        new(AiEnvironments.ClaudeCode, "Claude Code", "claude"),
        new(AiEnvironments.Cursor, "Cursor Agent", "cursor-agent"),
        new(AiEnvironments.Codex, "Codex CLI", "codex"),
        new(Gemini, "Gemini CLI", "gemini")
    ];
}
