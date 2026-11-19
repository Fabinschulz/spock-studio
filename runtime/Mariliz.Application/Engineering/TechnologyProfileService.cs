using System.Text.Json.Nodes;
using Mariliz.Application.Compatibility;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Engineering;

// Nada é escrito dentro do projeto.
public sealed class TechnologyProfileService(
    ConfigureProjectService configure,
    IntegrationHub hub,
    WorkspaceAuthorizer workspace,
    ITechnologyProfileStore store,
    IClock clock)
{
    public TechnologyProfile? Get(string projectId)
    {
        EnsureAuthorized(projectId);
        return store.Read(projectId);
    }

    public async Task<TechnologyProfile> RefreshAsync(string projectId, CancellationToken ct)
    {
        EnsureAuthorized(projectId);
        var report = await configure.DiscoverAsync(projectId, ct);
        var previous = store.Read(projectId);
        var profile = ProfileBuilder.Build(report, hub.Projects.List(projectId), previous, clock.UtcNow);
        store.Write(profile);

        var changes = profile.History.Count(change => change.At == profile.AnalyzedAt);
        hub.Audit.Record("engineering.profile_refreshed", new JsonObject
        {
            ["projectId"] = projectId,
            ["technologies"] = profile.Technologies.Count,
            ["changes"] = changes
        });
        return profile;
    }

    // Calculada a cada leitura: a mesma versão muda de situação quando o suporte acaba ou o catálogo é atualizado.
    public CompatibilityReport? Compatibility(TechnologyProfile? profile) =>
        profile is null ? null : CompatibilityAssessor.Assess(profile.Modules, ReleaseCatalog.Embedded, clock.UtcNow);

    private void EnsureAuthorized(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
    }
}
