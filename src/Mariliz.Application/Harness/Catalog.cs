using System.Reflection;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

public sealed record CatalogComponent(string Id, ComponentKind Kind, string Version, string Title);

/// <summary>Os componentes que a aplicação sabe gerar. A proposta do Harness Engineer só pode escolher entre eles.</summary>
public static class Catalog
{
    public const string ComponentVersion = "0.1.0";

    public const string AgentHarnessEngineer = "mariliz.agent.harness-engineer";
    public const string AgentCodeReviewer = "mariliz.agent.code-reviewer";
    public const string AgentVerifier = "mariliz.agent.verifier";
    public const string SkillHarnessContract = "mariliz.skill.harness-contract";
    public const string RuleGeneral = "mariliz.rule.general";
    public const string RuleStackDotnet = "mariliz.rule.stack-dotnet";
    public const string RuleStackJavascript = "mariliz.rule.stack-javascript";
    public const string RuleRepoCommands = "mariliz.rule.repo-commands";
    public const string HookPolicy = "mariliz.hook.policy";
    public const string SddTlc = "mariliz.sdd.tlc";
    public const string ConfigDeclaration = "mariliz.config.declaration";
    public const string CustomRulePrefix = "mariliz.rule.custom.";

    public static readonly IReadOnlyList<CatalogComponent> Components =
    [
        new(AgentHarnessEngineer, ComponentKind.Agent, ComponentVersion, "Harness Engineer"),
        new(AgentCodeReviewer, ComponentKind.Agent, ComponentVersion, "Code Reviewer"),
        new(AgentVerifier, ComponentKind.Agent, ComponentVersion, "Verifier"),
        new(SkillHarnessContract, ComponentKind.Skill, ComponentVersion, "Contrato de evidência"),
        new(RuleGeneral, ComponentKind.Rule, ComponentVersion, "Regras gerais de engenharia"),
        new(RuleStackDotnet, ComponentKind.Rule, ComponentVersion, "Regras .NET"),
        new(RuleStackJavascript, ComponentKind.Rule, ComponentVersion, "Regras JavaScript e TypeScript"),
        new(RuleRepoCommands, ComponentKind.Rule, ComponentVersion, "Comandos do repositório"),
        new(HookPolicy, ComponentKind.Hook, ComponentVersion, "Hook de política"),
        new(SddTlc, ComponentKind.Sdd, "3.3.0", "TLC (spec-driven)"),
        new(ConfigDeclaration, ComponentKind.Config, ComponentVersion, "Declaração e lock")
    ];

    public static CatalogComponent? Find(string id) => Components.FirstOrDefault(c => c.Id == id);

    /// <summary>Ids que a proposta do Harness Engineer pode citar.</summary>
    public static readonly IReadOnlyList<string> Proposable =
    [
        AgentHarnessEngineer, AgentCodeReviewer, AgentVerifier, SkillHarnessContract,
        RuleGeneral, RuleStackDotnet, RuleStackJavascript, RuleRepoCommands
    ];

    // Caminhos dos arquivos de cada componente.
    public const string PathHarnessEngineer = ".claude/agents/harness-engineer.md";
    public const string PathProposalSchema = ".mariliz/schemas/harness-proposal.v1.schema.json";
    public const string PathProposal = ".mariliz/proposal.json";
    public const string PathCodeReviewer = ".claude/agents/code-reviewer.md";
    public const string PathVerifier = ".claude/agents/verifier.md";
    public const string PathHarnessContract = ".claude/skills/harness-contract/SKILL.md";
    public const string PathRuleGeneral = ".claude/rules/mariliz-general.md";
    public const string PathRuleDotnet = ".claude/rules/mariliz-stack-dotnet.md";
    public const string PathRuleJavascript = ".claude/rules/mariliz-stack-javascript.md";
    public const string PathRuleRepo = ".claude/rules/mariliz-repo.md";
    public const string PathHookScript = ".claude/hooks/mariliz-policy.py";
    public const string PathSettings = ".claude/settings.json";
}

/// <summary>Conteúdo embutido no motor (ativos originais ou adaptados de fonte autorizada, MIT).</summary>
public static class AssetLoader
{
    private static readonly Assembly Assembly = typeof(AssetLoader).Assembly;

    public static string Read(string name)
    {
        var normalized = name.Replace('\\', '/');
        var resource = Assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.Replace('\\', '/').EndsWith("Assets/" + normalized, StringComparison.Ordinal)
                                 || r.Replace('\\', '/').EndsWith("." + normalized.Replace('/', '.'), StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Ativo embutido não encontrado: {name}");

        using var stream = Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
