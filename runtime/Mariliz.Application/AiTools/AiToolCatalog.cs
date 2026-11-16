using Mariliz.Application.Discovery;

namespace Mariliz.Application.AiTools;

/// <summary>
/// O modo somente leitura de cada CLI usa a trava da própria ferramenta, nunca a confiança no pedido: Claude Code em modo
/// de plano e sem as ferramentas de escrita e de shell; Codex com a sandbox só de leitura; Cursor Agent com a sandbox
/// ligada e sem <c>--force</c>; Gemini CLI sem <c>--yolo</c>, em que o modo não interativo nega o que pediria confirmação.
/// </summary>
public static class AiToolCatalog
{
    public const string Gemini = "gemini";

    public static readonly IReadOnlyList<AiToolDefinition> All =
    [
        new(AiEnvironments.ClaudeCode, "Claude Code", "claude", new AgentInvocation(
            ["-p", "--permission-mode", "plan", "--disallowedTools", "Edit,Write,NotebookEdit,Bash", "--output-format", "text"],
            PromptOnStandardInput: true, Credentials: ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_OAUTH_TOKEN"])),
        new(AiEnvironments.Cursor, "Cursor Agent", "cursor-agent", new AgentInvocation(
            ["--print", "--output-format", "text", "--sandbox", "enabled", "--workspace", AgentInvocation.ProjectFolder],
            PromptOnStandardInput: true, Credentials: ["CURSOR_API_KEY"])),
        new(AiEnvironments.Codex, "Codex CLI", "codex", new AgentInvocation(
            ["exec", "--sandbox", "read-only", "--skip-git-repo-check", "--color", "never", "-C", AgentInvocation.ProjectFolder, "-"],
            PromptOnStandardInput: true, Credentials: ["OPENAI_API_KEY", "CODEX_API_KEY"])),
        new(Gemini, "Gemini CLI", "gemini", new AgentInvocation(["-p"], PromptOnStandardInput: false,
            Credentials: ["GEMINI_API_KEY", "GOOGLE_API_KEY"]))
    ];
}
