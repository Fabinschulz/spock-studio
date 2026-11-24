using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Application.Audit;

namespace Mariliz.Application.Integrations;

// É aqui que o isolamento é decidido: toda leitura e toda mudança partem do projeto autorizado, e uma integração só é encontrada pelo projeto dono dela.
public sealed class ProjectIntegrationService(
    IIntegrationStore store,
    ISyncedDataStore data,
    ConnectionService connections,
    ConnectorRegistry connectors,
    WorkspaceAuthorizer workspace,
    SyncActivity activity,
    IClock clock,
    IAuditLog audit)
{
    public const int MaxPerProject = 16;

    public IReadOnlyList<ProjectIntegration> List(string projectId)
    {
        EnsureAuthorized(projectId);
        return store.Integrations().Where(i => i.ProjectId == projectId).ToList();
    }

    // De outro projeto, a integração simplesmente não existe.
    public ProjectIntegration Require(string projectId, string integrationId)
    {
        EnsureAuthorized(projectId);
        return store.Integrations().FirstOrDefault(i => i.Id == integrationId && i.ProjectId == projectId)
            ?? throw IntegrationErrors.IntegrationNotFound();
    }

    public SyncedData Data(ProjectIntegration integration) => data.Read(integration.ProjectId, integration.Id);

    // Confere com o provedor que a credencial alcança o recurso antes de vincular.
    public async Task<ProjectIntegration> LinkAsync(
        string projectId, string connectionId, string resource, IReadOnlyList<string> dataKinds, CancellationToken ct)
    {
        EnsureAuthorized(projectId);
        var connection = connections.Require(connectionId);
        var connector = connectors.Require(connection.Provider);
        var kinds = ValidKinds(connector, dataKinds);
        var normalized = connector.NormalizeResource(resource);
        if (List(projectId).Count >= MaxPerProject)
            throw IntegrationErrors.LimitReached("O limite de integrações deste projeto foi atingido.");

        var probe = await connector.ProbeResourceAsync(connections.AccessFor(connection), normalized, ct);
        if (List(projectId).Any(i => i.ConnectionId == connectionId && string.Equals(i.Scope.Resource, probe.Resource, StringComparison.OrdinalIgnoreCase)))
            throw IntegrationErrors.AlreadyLinked();

        var integration = new ProjectIntegration(
            "pint-" + Guid.NewGuid().ToString("N")[..12], projectId, connectionId, connection.Provider,
            new DataScope(probe.Resource, probe.Name, kinds), ContextualizationPolicy.Closed, clock.UtcNow);
        store.SaveIntegration(integration);
        Audit(AuditActions.IntegrationLinked, integration);
        return integration;
    }

    // O dado que ficou fora do novo recorte é apagado na hora.
    public ProjectIntegration Configure(string projectId, string integrationId, IReadOnlyList<string>? dataKinds, bool? feedsKnowledge)
    {
        var integration = Require(projectId, integrationId);
        if (activity.IsRunning(integrationId)) throw IntegrationErrors.SyncRunning();

        var updated = integration;
        if (dataKinds is not null)
            updated = updated with { Scope = updated.Scope with { DataKinds = ValidKinds(connectors.Require(integration.Provider), dataKinds) } };
        if (feedsKnowledge is { } feeds)
            updated = updated with { Policy = new ContextualizationPolicy(feeds) };

        store.SaveIntegration(updated);
        if (dataKinds is not null)
        {
            var synced = data.Read(projectId, integrationId);
            if (synced.RemoveKindsOutside(updated.Scope.DataKinds) > 0) data.Write(projectId, integrationId, synced);
        }
        Audit(AuditActions.IntegrationScopeChanged, updated);
        return updated;
    }

    public void Unlink(string projectId, string integrationId)
    {
        var integration = Require(projectId, integrationId);
        if (activity.IsRunning(integrationId)) throw IntegrationErrors.SyncRunning();

        store.RemoveIntegration(integrationId);
        data.Delete(projectId, integrationId);
        Audit(AuditActions.IntegrationUnlinked, integration);
    }

    private void EnsureAuthorized(string projectId)
    {
        if (!workspace.IsAuthorized(projectId)) throw EngineErrors.ProjectNotAuthorized();
    }

    private static List<string> ValidKinds(IIntegrationConnector connector, IReadOnlyList<string> requested)
    {
        var offered = connector.Descriptor.DataKinds.Select(k => k.Id).ToList();
        var kinds = requested.Distinct(StringComparer.Ordinal).ToList();
        if (kinds.Count == 0 || kinds.Any(kind => !offered.Contains(kind))) throw IntegrationErrors.ScopeInvalid();
        return kinds;
    }

    private void Audit(string action, ProjectIntegration integration) =>
        audit.Record(action, new JsonObject
        {
            ["projectId"] = integration.ProjectId,
            ["integrationId"] = integration.Id,
            ["connectionId"] = integration.ConnectionId,
            ["resource"] = integration.Scope.Resource,
            ["dataKinds"] = new JsonArray(integration.Scope.DataKinds.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray()),
            ["feedsKnowledge"] = integration.Policy.FeedsKnowledge
        });
}
