namespace Mariliz.Application.Discovery;

public static class AiEnvironments
{
    public const string ClaudeCode = "claude-code";
    public const string Cursor = "cursor";
    public const string Codex = "codex";

    // No Cursor e no Codex o Harness grava só a ligação com a IDE e a skill do contrato (C3).
    public static readonly IReadOnlyList<string> Supported = [ClaudeCode, Cursor, Codex];
    public static readonly IReadOnlyList<string> Known = [ClaudeCode, Cursor, Codex];
}
