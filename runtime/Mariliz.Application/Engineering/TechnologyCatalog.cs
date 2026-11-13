using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Engineering;

// É o único lugar com regra por tecnologia fora dos detectores; a interface só mostra o que vem daqui.
public static class TechnologyCatalog
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        ["dotnet"] = "Plataforma e runtime",
        ["node"] = "Runtime",
        ["nextjs"] = "Framework web",
        ["react"] = "Biblioteca de interface",
        ["angular"] = "Framework web",
        ["flutter"] = "Framework de aplicativo"
    };

    public static string Category(string id) => Categories.GetValueOrDefault(id, "Outra");

    // A mesma decisão que o plano do Harness toma.
    public static ProfileHarnessGuidance Guidance(ProfileTechnology technology) =>
        technology.Id == "dotnet"
            ? new(technology.Name, Catalog.RuleStackDotnet, true, "Regra da stack .NET, aplicada aos arquivos .cs e .csproj.")
            : HarnessComposition.JavascriptStacks.Contains(technology.Id)
                ? new(technology.Name, Catalog.RuleStackJavascript, true, "Regra da stack JavaScript e TypeScript, aplicada aos arquivos .ts, .tsx, .js e .jsx.")
                : new(technology.Name, null, false, "Ainda não há perfil de regras para esta tecnologia; o plano registra a limitação.");

    public static string ConnectionRole(string provider) => provider switch
    {
        IntegrationProviders.GitHub => "Repositório",
        IntegrationProviders.Jira => "Gestão de trabalho",
        _ => "Integração"
    };
}
