using Mariliz.Application.Harness;

namespace Mariliz.Application.Engineering;

public static class HarnessLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [Catalog.AgentHarnessEngineer] = "Agente Harness Engineer",
        [Catalog.AgentCodeReviewer] = "Agente revisor de código",
        [Catalog.AgentVerifier] = "Agente verificador",
        [Catalog.SkillHarnessContract] = "Skill do contrato de evidências",
        [Catalog.RuleGeneral] = "Regras gerais",
        [Catalog.RuleStackDotnet] = "Regras da stack .NET",
        [Catalog.RuleStackJavascript] = "Regras da stack JavaScript e TypeScript",
        [Catalog.RuleRepoCommands] = "Comandos do repositório",
        [Catalog.RuleArchitecture] = "Documentos de arquitetura",
        [Catalog.HookPolicy] = "Hook de política",
        [Catalog.HookSession] = "Hook de início de sessão",
        [Catalog.HookPrompt] = "Hook por prompt",
        [Catalog.IdeCodexBootstrap] = "Bloco do Spock no AGENTS.md",
        [Catalog.IdeCodexMcp] = "Servidor MCP do Spock (.codex/config.toml)",
        [Catalog.HookCodexSession] = "Hook de início de sessão do Codex",
        [Catalog.HookCodexPrompt] = "Hook por prompt do Codex",
        [Catalog.IdeCursorRule] = "Regra do Spock no Cursor (.cursor/rules/spock.mdc)",
        [Catalog.IdeCursorMcp] = "Servidor MCP do Spock (.cursor/mcp.json)",
        [Catalog.HookCursorSession] = "Hook de início de sessão do Cursor",
        [Catalog.SkillAgentsHarnessContract] = "Skill do contrato de evidências (.agents/skills)",
        [Catalog.IdeClaudeBootstrap] = "Bloco do Spock no CLAUDE.md",
        [Catalog.IdeMcpServer] = "Servidor MCP do Spock (.mcp.json)",
        [Catalog.SpecificationStructure] = "Estrutura de especificação",
        [Catalog.ConfigDeclaration] = "Declaração do projeto"
    };

    public static string Of(string id) =>
        Labels.TryGetValue(id, out var label) ? label
        : id.StartsWith(Catalog.CustomRulePrefix, StringComparison.Ordinal) ? $"Regra do projeto: {id[Catalog.CustomRulePrefix.Length..]}"
        : id;
}
