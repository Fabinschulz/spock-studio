using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Infrastructure.Integrations;

/// <summary>A central de integrações sobre o disco local e a rede real. Os conectores desta versão são registrados aqui.</summary>
public static class LocalIntegrationHub
{
    public static IntegrationHub Create(
        string dataRoot, WorkspaceAuthorizer workspace, IClock clock, IConnectorTransport transport,
        Action<string, int, string, System.Text.Json.Nodes.JsonObject, DateTimeOffset>? emit = null)
    {
        var client = new ConnectorClient(transport, clock);
        var connectors = new ConnectorRegistry([new GitHubConnector(client), new JiraConnector(client, clock)]);
        return new IntegrationHub(
            new FileIntegrationStore(dataRoot), new FileSyncedDataStore(dataRoot), SecretVault(dataRoot), connectors, workspace, clock,
            new FileAuditLog(dataRoot, clock), emit);
    }

    /// <summary>O cofre do sistema em que o motor está rodando: DPAPI no Windows, Chaveiro no macOS, serviço de segredos no Linux.</summary>
    public static ISecretVault SecretVault(string dataRoot) => SecretVaults.For(SecretVaults.CurrentPlatform(), dataRoot, new SystemProcessRunner());
}
