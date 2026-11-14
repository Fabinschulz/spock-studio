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
        [Catalog.SpecificationStructure] = "Estrutura de especificação",
        [Catalog.ConfigDeclaration] = "Declaração do projeto (mariliz.yaml)"
    };

    public static string Of(string id) =>
        Labels.TryGetValue(id, out var label) ? label
        : id.StartsWith(Catalog.CustomRulePrefix, StringComparison.Ordinal) ? $"Regra do projeto: {id[Catalog.CustomRulePrefix.Length..]}"
        : id;
}
