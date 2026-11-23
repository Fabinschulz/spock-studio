using Mariliz.Application.Workflows;

namespace Mariliz.Application.Harness;

/// <param name="Stack">O nome curto no arquivo (<c>spock-patterns-react.md</c>).</param>
/// <param name="Technologies">As stacks que pedem a regra; os caminhos da regra são os das pastas delas.</param>
/// <param name="Selects">Os padrões do catálogo da Organização que entram na regra.</param>
public sealed record PatternRuleProfile(
    string Component,
    string Stack,
    string Name,
    IReadOnlyList<string> Technologies,
    string[] Globs,
    Func<ReferencePattern, bool> Selects);

// W3-1: a regra da linguagem (stack-javascript, stack-dotnet) diz como escrever código; esta diz quais padrões da Organização
// valem para o framework. React e Angular deixam de receber o mesmo texto.
public static class PatternRuleProfiles
{
    // A §6.9 do catálogo é de integração com sistema externo: só entra inteira quando o projeto integra.
    public const string IntegrationSubsection = "6.9 ";

    private static readonly string[] Frontend = ["react", "nextjs", "angular"];
    private static readonly string[] Backend = ["dotnet", "python", "java", "go", "rust"];
    // Um package.json sem React nem Angular vira a stack node, e a raiz de muito monorepo de frontend é só isso (scripts e
    // ferramentas). Node só conta como backend com um framework de servidor.
    private static readonly string[] NodeServers = ["express", "fastify", "nestjs"];

    public static readonly PatternRuleProfile React = new(Catalog.RulePatternsReact, "react", "React",
        ["react", "nextjs"], ["**/*.tsx", "**/*.ts", "**/*.jsx", "**/*.js"], pattern => Covers(pattern, "react"));

    public static readonly PatternRuleProfile Angular = new(Catalog.RulePatternsAngular, "angular", "Angular",
        ["angular"], ["**/*.ts", "**/*.html", "**/*.scss"], pattern => Covers(pattern, "angular"));

    public static readonly PatternRuleProfile Dotnet = new(Catalog.RulePatternsDotnet, "dotnet", ".NET",
        ["dotnet"], ["**/*.cs", "**/*.csproj"], pattern => Covers(pattern, "dotnet"));

    // Sem caminho: contrato entre front e back, CI e ambiente local não moram numa extensão de arquivo.
    public static readonly PatternRuleProfile Fullstack = new(Catalog.RulePatternsFullstack, "fullstack", "fullstack",
        [], [], pattern => pattern.Section.StartsWith("Fullstack", StringComparison.Ordinal));

    public static readonly IReadOnlyList<PatternRuleProfile> All = [React, Angular, Dotnet, Fullstack];

    // Fullstack é frontend e backend no mesmo repositório; os outros valem quando uma das stacks deles aparece.
    public static bool Applies(PatternRuleProfile profile, IReadOnlyCollection<string> stacks, IReadOnlyCollection<string> frameworks) =>
        profile == Fullstack
            ? stacks.Any(Frontend.Contains) && (stacks.Any(Backend.Contains) || stacks.Contains("node") && frameworks.Any(NodeServers.Contains))
            : stacks.Any(profile.Technologies.Contains);

    public static bool Wants(PatternRuleProfile profile, string stackId) =>
        profile == Fullstack ? Frontend.Contains(stackId) || Backend.Contains(stackId) : profile.Technologies.Contains(stackId);

    private static bool Covers(ReferencePattern pattern, string technology) => pattern.Technologies.Contains(technology);
}
