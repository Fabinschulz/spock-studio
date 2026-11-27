using Mariliz.Application.Boot;
using Mariliz.Application.Context;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// O Boot da IDE (spock_boot) é montado por chamada, como o Context Engine: o registro de pastas é relido e um projeto aberto no
// Spock depois de a IDE iniciar o servidor já vale. O que o Boot enxerga é decidido em McpBootComposer; aqui só entram as peças locais.
internal static class BootComposition
{
    public static BootService Create(string projectId, WorkspaceAuthorizer workspace, string dataRoot, IClock clock, IAuditLog audit, IConnectorTransport transport,
        ITokenizers tokenizers, EngineComposition composition)
    {
        var integrations = LocalIntegrationHub.Create(dataRoot, workspace, clock, transport);
        return McpBootComposer.Create(projectId, workspace, clock, audit, new FileBootStore(dataRoot),
            ContextComposition.Engine(workspace, dataRoot, clock, audit, transport, tokenizers), new LocalProjectFilesFactory(),
            new FileTechnologyProfileStore(dataRoot), new FileRunStore(dataRoot), integrations, LocalCloudHub.Create(dataRoot, workspace, clock, audit),
            composition.Management(workspace, integrations));
    }
}
