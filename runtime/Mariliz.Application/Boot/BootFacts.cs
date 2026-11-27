using Mariliz.Application.Engineering;
using Mariliz.Application.Integrations;
using Mariliz.Application.Cloud;
using Mariliz.Application.Models;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Boot;

/// <summary>O que é verdade agora sobre o projeto e a máquina; é a única entrada de disponibilidade e de permissão.</summary>
public sealed record BootFacts(
    string? ProjectId,
    bool ProjectAuthorized,
    string? ProjectName,
    IReadOnlySet<string> RoutedMethods,
    bool ModelConfigured,
    int IntegrationLinks,
    int PortfolioIntegrationLinks,
    int CloudLinks,
    bool HarnessApplied,
    DateTimeOffset? ProfileAnalyzedAt,
    IReadOnlyList<string> Unknown);

/// <summary>
/// Lê os serviços que já existem; não guarda nada. Um serviço ausente nesta sessão do motor vira "desconhecido" nos fatos, e
/// a capacidade que depende dele aparece como indisponível em vez de ser dada como pronta.
/// </summary>
public sealed class BootFactsProbe(
    WorkspaceAuthorizer workspace,
    ModelGateway? models = null,
    IntegrationHub? integrations = null,
    CloudHub? cloud = null,
    HarnessStatusService? harness = null,
    ITechnologyProfileStore? profiles = null)
{
    public BootFacts Read(string? projectId, IReadOnlySet<string> routedMethods)
    {
        var unknown = new List<string>();
        var authorized = projectId is not null && workspace.IsAuthorized(projectId);
        var name = authorized ? workspace.DisplayNameOf(projectId!) : null;

        var modelConfigured = false;
        if (models is null) unknown.Add("model");
        else modelConfigured = models.Status().Configured;

        var links = 0;
        var portfolio = 0;
        if (integrations is null) unknown.Add("integrations");
        else
        {
            if (authorized) links = integrations.Projects.List(projectId!).Count;
            portfolio = workspace.Projects().Sum(project => integrations.Projects.List(project.ProjectId).Count);
        }

        var cloudLinks = 0;
        if (cloud is null) unknown.Add("cloud");
        else if (authorized) cloudLinks = cloud.Projects.List(projectId!).Count;

        var harnessApplied = false;
        if (harness is null) unknown.Add("harness");
        else if (authorized) harnessApplied = harness.Inspect(projectId!).Applied;

        DateTimeOffset? analyzed = null;
        if (profiles is null) unknown.Add("profile");
        else if (authorized) analyzed = profiles.Read(projectId!)?.AnalyzedAt;

        return new BootFacts(projectId, authorized, name, routedMethods, modelConfigured, links, portfolio, cloudLinks, harnessApplied, analyzed, unknown);
    }
}
