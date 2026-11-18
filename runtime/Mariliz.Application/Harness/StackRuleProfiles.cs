using Mariliz.Application.Discovery;

namespace Mariliz.Application.Harness;

// Uma família de regras por linha. Qual tecnologia pertence a qual família é do registro (Technologies), não daqui.
public static class StackRuleProfiles
{
    public static readonly StackRuleProfile Dotnet = new(RuleFamily.Dotnet, Catalog.RuleStackDotnet, "rules/stack-dotnet.md",
        ["**/*.cs", "**/*.csproj"], ".NET", "Regra da stack .NET, aplicada aos arquivos .cs e .csproj.");

    public static readonly StackRuleProfile Javascript = new(RuleFamily.Javascript, Catalog.RuleStackJavascript, "rules/stack-javascript.md",
        ["**/*.ts", "**/*.tsx", "**/*.js", "**/*.jsx"], "JavaScript/TypeScript",
        "Regra da stack JavaScript e TypeScript, aplicada aos arquivos .ts, .tsx, .js e .jsx.");

    public static readonly IReadOnlyList<StackRuleProfile> All = [Dotnet, Javascript];

    public static StackRuleProfile? Of(string? stackId) => All.FirstOrDefault(profile => profile.Family == Technologies.RulesOf(stackId));
}
