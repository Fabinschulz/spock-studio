namespace Mariliz.Application.Discovery;

public static class AiEnvironments
{
    public const string ClaudeCode = "claude-code";
    public const string Cursor = "cursor";
    public const string Codex = "codex";

    // Os demais são só detectados.
    public static readonly IReadOnlyList<string> Supported = [ClaudeCode];
    public static readonly IReadOnlyList<string> Known = [ClaudeCode, Cursor, Codex];
}
