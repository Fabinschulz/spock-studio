using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>A central de integrações sobre o disco local e a rede real. Os conectores desta versão são registrados aqui.</summary>
public static class LocalIntegrationHub
{
    public static IntegrationHub Create(string dataRoot, WorkspaceAuthorizer workspace, IClock clock, IConnectorTransport transport)
    {
        var client = new ConnectorClient(transport, clock);
        var connectors = new ConnectorRegistry([new GitHubConnector(client), new JiraConnector(client, clock)]);
        return new IntegrationHub(
            new FileIntegrationStore(dataRoot), new FileSyncedDataStore(dataRoot), SecretVault(dataRoot), connectors, workspace, clock,
            new FileAuditLog(dataRoot, clock));
    }

    public static ISecretVault SecretVault(string dataRoot) =>
        OperatingSystem.IsWindows() ? new DpapiSecretVault(dataRoot) : new UnavailableSecretVault();
}
