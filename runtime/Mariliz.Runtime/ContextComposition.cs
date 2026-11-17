using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// O mesmo Context Engine para o MCP (spock_context_bootstrap) e para o hook de sessão (context session): a IDE recebe o
// mesmo pacote, com a mesma versão, pelos dois caminhos. Listar vínculos não lê conta nenhuma.
internal static class ContextComposition
{
    public static ContextEngine Engine(WorkspaceAuthorizer workspace, string dataRoot, IClock clock, IAuditLog audit, IConnectorTransport transport,
        ITokenizers tokenizers) =>
        new(workspace, new HarnessStatusService(workspace, new LocalProjectFilesFactory(), clock), new FileTechnologyProfileStore(dataRoot), tokenizers, clock,
            LocalIntegrationHub.Create(dataRoot, workspace, clock, transport), LocalCloudHub.Create(dataRoot, workspace, clock, audit));
}
