using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Integrations;

public sealed class IntegrationHub
{
    public IntegrationHub(
        IIntegrationStore store,
        ISyncedDataStore data,
        ISecretVault vault,
        ConnectorRegistry connectors,
        WorkspaceAuthorizer workspace,
        IClock clock,
        IAuditLog audit,
        Action<string, int, string, System.Text.Json.Nodes.JsonObject, DateTimeOffset>? emit = null)
    {
        var activity = new SyncActivity();
        Audit = audit;
        Connectors = connectors;
        Activity = activity;
        VaultAvailable = vault.Available;
        VaultName = vault.Name;
        VaultHint = vault.Available ? null : vault.UnavailableHint;
        Connections = new ConnectionService(store, vault, connectors, clock, audit);
        Projects = new ProjectIntegrationService(store, data, Connections, connectors, workspace, activity, clock, audit);
        Reach = new ConnectionReachService(store, connectors, workspace);
        ExternalKnowledge = new ExternalKnowledge(store, data, connectors, workspace);
        Sync = new SyncService(Projects, Connections, connectors, data, activity, clock, audit, emit);
    }

    public ConnectorRegistry Connectors { get; }
    public ConnectionService Connections { get; }
    public ProjectIntegrationService Projects { get; }
    public ConnectionReachService Reach { get; }

    public ExternalKnowledge ExternalKnowledge { get; }
    public SyncService Sync { get; }
    public SyncActivity Activity { get; }
    public bool VaultAvailable { get; }

    public string VaultName { get; }

    public string? VaultHint { get; }

    public IAuditLog Audit { get; }

    public IAuditReader? AuditReader => Audit as IAuditReader;
}
