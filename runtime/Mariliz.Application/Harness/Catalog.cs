using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

// A proposta do Harness Engineer só pode escolher entre estes componentes.
public static class Catalog
{
    // SemVer por componente: sobe quando muda o texto que o componente grava num projeto já configurado.
    // 0.1.1: o Harness Engineer e os cabeçalhos dizem Spock, e o hook protege os nomes spock e mariliz.
    public const string ComponentVersion = "0.1.0";

    public const string AgentHarnessEngineer = "mariliz.agent.harness-engineer";
    public const string AgentCodeReviewer = "mariliz.agent.code-reviewer";
    public const string AgentVerifier = "mariliz.agent.verifier";
    public const string SkillHarnessContract = "mariliz.skill.harness-contract";
    public const string RuleGeneral = "mariliz.rule.general";
    public const string RuleStackDotnet = "mariliz.rule.stack-dotnet";
    public const string RuleStackJavascript = "mariliz.rule.stack-javascript";
    public const string RuleRepoCommands = "mariliz.rule.repo-commands";
    public const string RuleArchitecture = "mariliz.rule.architecture";
    // W3-1: padrões da pasta Organização por framework, gerados do catálogo (PatternRuleProfiles).
    public const string RulePatternsReact = "mariliz.rule.patterns-react";
    public const string RulePatternsAngular = "mariliz.rule.patterns-angular";
    public const string RulePatternsDotnet = "mariliz.rule.patterns-dotnet";
    public const string RulePatternsFullstack = "mariliz.rule.patterns-fullstack";
    // W3-4: a organização que o código já tem, da análise de arquitetura (ProjectConventions).
    public const string RuleProjectConventions = "mariliz.rule.project-conventions";
    // W3-2: skill curta por stack, com o "não copiar" do catálogo e um checklist (StackSkillProfiles).
    public const string SkillStackReact = "mariliz.skill.stack-react";
    public const string SkillStackAngular = "mariliz.skill.stack-angular";
    public const string SkillStackDotnet = "mariliz.skill.stack-dotnet";
    public const string HookPolicy = "mariliz.hook.policy";
    // C2: o que liga o Claude Code ao Spock. Ficam nos arquivos da equipe, só o trecho do Spock (ManagedRegion).
    public const string HookSession = "mariliz.hook.session";
    public const string HookPrompt = "mariliz.hook.prompt";
    public const string IdeClaudeBootstrap = "mariliz.ide.claude-bootstrap";
    public const string IdeMcpServer = "mariliz.ide.mcp";
    // C3: o mesmo para o Codex, no AGENTS.md, no .codex/config.toml e no .codex/hooks.json.
    public const string IdeCodexBootstrap = "mariliz.ide.codex-bootstrap";
    public const string IdeCodexMcp = "mariliz.ide.codex-mcp";
    public const string HookCodexSession = "mariliz.hook.codex-session";
    public const string HookCodexPrompt = "mariliz.hook.codex-prompt";
    // C3: o Cursor tem regra própria em .cursor/rules, MCP em .cursor/mcp.json e só o hook de sessão.
    public const string IdeCursorRule = "mariliz.ide.cursor-rule";
    public const string IdeCursorMcp = "mariliz.ide.cursor-mcp";
    public const string HookCursorSession = "mariliz.hook.cursor-session";
    // C3: a mesma skill do contrato em .agents/skills, a pasta que o Codex e o Cursor leem.
    public const string SkillAgentsHarnessContract = "mariliz.skill.agents-harness-contract";
    public const string SpecificationStructure = "mariliz.specification.structure";
    public const string ConfigDeclaration = "mariliz.config.declaration";
    public const string CustomRulePrefix = "mariliz.rule.custom.";

    public static readonly IReadOnlyList<CatalogComponent> Components =
    [
        new(AgentHarnessEngineer, ComponentKind.Agent, "0.1.1", "Harness Engineer"),
        new(AgentCodeReviewer, ComponentKind.Agent, ComponentVersion, "Code Reviewer"),
        new(AgentVerifier, ComponentKind.Agent, ComponentVersion, "Verifier"),
        new(SkillHarnessContract, ComponentKind.Skill, ComponentVersion, "Contrato de evidência"),
        new(RuleGeneral, ComponentKind.Rule, ComponentVersion, "Regras gerais de engenharia"),
        new(RuleStackDotnet, ComponentKind.Rule, ComponentVersion, "Regras .NET"),
        new(RuleStackJavascript, ComponentKind.Rule, ComponentVersion, "Regras JavaScript e TypeScript"),
        new(RuleRepoCommands, ComponentKind.Rule, ComponentVersion, "Comandos do repositório"),
        new(RuleArchitecture, ComponentKind.Rule, ComponentVersion, "Documentos de arquitetura"),
        new(RulePatternsReact, ComponentKind.Rule, ComponentVersion, "Padrões React da Organização"),
        new(RulePatternsAngular, ComponentKind.Rule, ComponentVersion, "Padrões Angular da Organização"),
        new(RulePatternsDotnet, ComponentKind.Rule, ComponentVersion, "Padrões .NET da Organização"),
        new(RulePatternsFullstack, ComponentKind.Rule, ComponentVersion, "Padrões fullstack da Organização"),
        new(RuleProjectConventions, ComponentKind.Rule, ComponentVersion, "Convenções do projeto"),
        new(SkillStackReact, ComponentKind.Skill, ComponentVersion, "Skill React"),
        new(SkillStackAngular, ComponentKind.Skill, ComponentVersion, "Skill Angular"),
        new(SkillStackDotnet, ComponentKind.Skill, ComponentVersion, "Skill .NET"),
        new(HookPolicy, ComponentKind.Hook, "0.1.1", "Hook de política"),
        new(HookSession, ComponentKind.Hook, ComponentVersion, "Hook de início de sessão"),
        new(HookPrompt, ComponentKind.Hook, ComponentVersion, "Hook por prompt"),
        new(IdeClaudeBootstrap, ComponentKind.Rule, ComponentVersion, "Bloco do Spock no CLAUDE.md"),
        new(IdeMcpServer, ComponentKind.Tool, ComponentVersion, "Servidor MCP do Spock"),
        new(IdeCodexBootstrap, ComponentKind.Rule, ComponentVersion, "Bloco do Spock no AGENTS.md"),
        new(IdeCodexMcp, ComponentKind.Tool, ComponentVersion, "Servidor MCP do Spock no Codex"),
        new(HookCodexSession, ComponentKind.Hook, ComponentVersion, "Hook de início de sessão do Codex"),
        new(HookCodexPrompt, ComponentKind.Hook, ComponentVersion, "Hook por prompt do Codex"),
        new(IdeCursorRule, ComponentKind.Rule, ComponentVersion, "Regra do Spock no Cursor"),
        new(IdeCursorMcp, ComponentKind.Tool, ComponentVersion, "Servidor MCP do Spock no Cursor"),
        new(HookCursorSession, ComponentKind.Hook, ComponentVersion, "Hook de início de sessão do Cursor"),
        new(SkillAgentsHarnessContract, ComponentKind.Skill, ComponentVersion, "Contrato de evidência em .agents/skills"),
        // Externo: a versão real vem da instalação verificada (ver LockBuilder), não do catálogo.
        new(SpecificationStructure, ComponentKind.Specification, "", "Estrutura de especificação"),
        new(ConfigDeclaration, ComponentKind.Config, "0.1.1", "Declaração e lock")
    ];

    public static CatalogComponent? Find(string id) => Components.FirstOrDefault(c => c.Id == id);

    // O ativo embutido que o componente grava no projeto, num lugar só: o plano, as fichas e a política leem daqui.
    public static string Asset(string componentId) => componentId switch
    {
        AgentHarnessEngineer => "agents/harness-engineer.md",
        AgentCodeReviewer => "agents/code-reviewer.md",
        AgentVerifier => "agents/verifier.md",
        SkillHarnessContract or SkillAgentsHarnessContract => "skills/harness-contract/SKILL.md",
        RuleGeneral => "rules/general.md",
        HookPolicy => "hooks/mariliz-policy.py",
        _ => StackRuleProfiles.All.FirstOrDefault(profile => profile.Component == componentId)?.Asset
             ?? throw new InvalidOperationException($"O componente {componentId} não grava um ativo único.")
    };

    public static readonly IReadOnlyList<string> Proposable =
    [
        AgentHarnessEngineer, AgentCodeReviewer, AgentVerifier, SkillHarnessContract,
        RuleGeneral, RuleStackDotnet, RuleStackJavascript, RuleRepoCommands
    ];

    // Os caminhos com o nome do produto (regras, hook, declaração, lock, proposta) vêm de HarnessNaming, por projeto.
    public const string PathSettings = ".claude/settings.json";
    public const string PathClaudeMd = "CLAUDE.md";
    public const string PathMcpJson = ".mcp.json";
    public const string PathAgentsMd = "AGENTS.md";
    public const string PathCodexConfig = ".codex/config.toml";
    public const string PathCodexHooks = ".codex/hooks.json";
    public const string PathCursorRule = ".cursor/rules/spock.mdc";
    public const string PathCursorMcp = ".cursor/mcp.json";
    public const string PathCursorHooks = ".cursor/hooks.json";

    // Arquivo do componente, no nome que este projeto usa. Nulo para o que não tem arquivo próprio.
    public static IReadOnlyList<string> FilesOf(string componentId, HarnessNaming naming) => componentId switch
    {
        AgentHarnessEngineer => [naming.HarnessEngineerAgent, naming.ProposalSchema],
        AgentCodeReviewer => [naming.CodeReviewerAgent],
        AgentVerifier => [naming.VerifierAgent],
        SkillHarnessContract => [naming.HarnessContractSkill],
        SkillAgentsHarnessContract => [naming.AgentsHarnessContractSkill],
        RuleGeneral => [naming.RuleGeneral],
        RuleStackDotnet => [naming.RuleDotnet],
        RuleStackJavascript => [naming.RuleJavascript],
        RuleRepoCommands => [naming.RuleRepo],
        RuleArchitecture => [naming.RuleArchitecture],
        RuleProjectConventions => [naming.RuleProjectConventions],
        SkillStackReact or SkillStackAngular or SkillStackDotnet =>
            [naming.StackSkill(StackSkillProfiles.All.Single(profile => profile.Component == componentId).Stack)],
        RulePatternsReact or RulePatternsAngular or RulePatternsDotnet or RulePatternsFullstack =>
            [naming.PatternRule(PatternRuleProfiles.All.Single(profile => profile.Component == componentId).Stack)],
        HookPolicy => [naming.HookScript, PathSettings],
        HookSession or HookPrompt => [PathSettings],
        IdeClaudeBootstrap => [PathClaudeMd],
        IdeMcpServer => [PathMcpJson],
        IdeCodexBootstrap => [PathAgentsMd],
        IdeCodexMcp => [PathCodexConfig],
        HookCodexSession or HookCodexPrompt => [PathCodexHooks],
        IdeCursorRule => [PathCursorRule],
        IdeCursorMcp => [PathCursorMcp],
        HookCursorSession => [PathCursorHooks],
        ConfigDeclaration => [naming.Declaration, naming.Lock],
        _ => []
    };

    // O inverso de FilesOf, nos dois nomes: um caminho gerenciado leva ao componente dono.
    public static string? OwnerOf(string path) =>
        HarnessNaming.All.SelectMany(naming => Components.Where(c => FilesOf(c.Id, naming).Contains(path))).Select(c => c.Id).FirstOrDefault();
}
