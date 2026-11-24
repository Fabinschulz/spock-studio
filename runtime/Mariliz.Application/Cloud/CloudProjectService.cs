using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Application.Audit;

namespace Mariliz.Application.Cloud;

// O vínculo diz o que um projeto pode ler de uma conexão e se agentes podem pedir essas leituras.
public sealed class CloudProjectService(
    ICloudStore store, CloudConnectionService connections, CloudProviderRegistry providers, WorkspaceAuthorizer workspace, IClock clock, IAuditLog audit)
{
    public const int MaxLinksPerProject = 8;

    private readonly object _gate = new();

    public IReadOnlyList<CloudProjectLink> List(string projectId)
    {
        RequireProject(projectId);
        return store.Links().Where(l => l.ProjectId == projectId).ToList();
    }

    public CloudProjectLink Link(string projectId, string connectionId, IReadOnlyList<string> capabilities, bool agentAccess)
    {
        RequireProject(projectId);
        var connection = connections.Require(connectionId);
        var clean = Capabilities(connection.Provider, capabilities);
        CloudProjectLink link;
        lock (_gate)
        {
            var existing = store.Links().Where(l => l.ProjectId == projectId).ToList();
            if (existing.Any(l => l.ConnectionId == connectionId)) throw CloudErrors.AlreadyLinked();
            if (existing.Count >= MaxLinksPerProject)
                throw CloudErrors.LimitReached($"Um projeto tem no máximo {MaxLinksPerProject} conexões cloud.");
            link = new CloudProjectLink(CloudConnectionService.NewId("clink-"), projectId, connectionId, connection.Provider, clean, agentAccess, clock.UtcNow);
            store.SaveLink(link);
        }
        Audit(AuditActions.CloudLinked, link, connection.Name);
        return link;
    }

    public CloudProjectLink Configure(string projectId, string linkId, IReadOnlyList<string>? capabilities, bool? agentAccess)
    {
        var before = Require(projectId, linkId);
        var after = before with
        {
            Capabilities = capabilities is null ? before.Capabilities : Capabilities(before.Provider, capabilities),
            AgentAccess = agentAccess ?? before.AgentAccess
        };
        lock (_gate) store.SaveLink(after);
        audit.Record(AuditActions.CloudScopeChanged, new JsonObject
        {
            ["projectId"] = projectId, ["linkId"] = linkId, ["connectionId"] = after.ConnectionId,
            ["capabilities"] = new JsonArray(after.Capabilities.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()),
            ["agentAccessBefore"] = before.AgentAccess, ["agentAccess"] = after.AgentAccess
        });
        return after;
    }

    public void Unlink(string projectId, string linkId)
    {
        var link = Require(projectId, linkId);
        lock (_gate) store.RemoveLink(linkId);
        Audit(AuditActions.CloudUnlinked, link, null);
    }

    public CloudProjectLink Require(string projectId, string linkId)
    {
        RequireProject(projectId);
        return store.Links().FirstOrDefault(l => l.Id == linkId && l.ProjectId == projectId) ?? throw CloudErrors.LinkNotFound();
    }

    // Para o MCP: vínculos liberados para agentes, sem conferir projeto (quem chama filtra pelo escopo dele).
    public IReadOnlyList<CloudProjectLink> AgentLinks() => store.Links().Where(l => l.AgentAccess && l.Capabilities.Count > 0).ToList();

    public CloudProjectLink? Find(string projectId, string connectionId) =>
        store.Links().FirstOrDefault(l => l.ProjectId == projectId && l.ConnectionId == connectionId);

    public void RequireProject(string projectId)
    {
        workspace.RequireRoot(projectId);
    }

    private IReadOnlyList<string> Capabilities(string provider, IReadOnlyList<string> asked)
    {
        foreach (var id in asked) providers.RequireCapability(provider, id);
        return asked.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private void Audit(string action, CloudProjectLink link, string? connectionName) =>
        audit.Record(action, new JsonObject
        {
            ["projectId"] = link.ProjectId, ["linkId"] = link.Id, ["connectionId"] = link.ConnectionId, ["provider"] = link.Provider,
            ["name"] = connectionName, ["capabilities"] = link.Capabilities.Count, ["agentAccess"] = link.AgentAccess
        });
}
