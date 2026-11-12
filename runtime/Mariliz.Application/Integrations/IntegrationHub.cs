using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Integrations;

/// <summary>Os casos de uso da central de integrações, montados sobre as mesmas portas.</summary>
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
        Connections = new ConnectionService(store, vault, connectors, clock, audit);
        Projects = new ProjectIntegrationService(store, data, Connections, connectors, workspace, activity, clock, audit);
        Sync = new SyncService(Projects, Connections, connectors, data, activity, clock, audit, emit);
    }

    public ConnectorRegistry Connectors { get; }
    public ConnectionService Connections { get; }
    public ProjectIntegrationService Projects { get; }
    public SyncService Sync { get; }
    public SyncActivity Activity { get; }
    public bool VaultAvailable { get; }

    /// <summary>A mesma trilha de auditoria, para outros casos de uso que precisam registrar operações.</summary>
    public IAuditLog Audit { get; }
}
