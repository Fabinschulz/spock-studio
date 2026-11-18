using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;

namespace Mariliz.Application.Engineering;

// O que a interface mostra por tecnologia, lido do registro (Technologies) e das famílias de regra do Harness.
public static class TechnologyCatalog
{
    public static string Category(string id) => Technologies.Find(id)?.Category ?? Frameworks.Category(id) ?? "Outra";

    // A mesma decisão que o plano do Harness toma.
    public static ProfileHarnessGuidance Guidance(ProfileTechnology technology) =>
        StackRuleProfiles.Of(technology.Id) is { } profile
            ? new(technology.Name, profile.Component, true, profile.Guidance)
            : new(technology.Name, null, false, "Ainda não há perfil de regras para esta tecnologia; o plano registra a limitação.");

    public static string ConnectionRole(string provider) => provider switch
    {
        IntegrationProviders.GitHub => "Repositório",
        IntegrationProviders.Jira => "Gestão de trabalho",
        _ => "Integração"
    };
}
