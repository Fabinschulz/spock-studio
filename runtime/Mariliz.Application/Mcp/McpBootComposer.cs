using Mariliz.Application.Boot;
using Mariliz.Application.Cloud;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;

namespace Mariliz.Application.Mcp;

// Monta o Boot de uma chamada do MCP. Quem chama passa as peças já prontas; aqui só se decide o que o Boot enxerga: as leituras do
// projeto (McpBootDispatcher), sem modelo, sem sessão de protocolo e sem nenhum serviço que grave no projeto.
public static class McpBootComposer
{
    public static BootService Create(string projectId, WorkspaceAuthorizer workspace, IClock clock, IAuditLog audit, IBootStore store, ContextEngine context,
        IProjectFilesFactory files, ITechnologyProfileStore profiles, IRunStore runs, IntegrationHub integrations, CloudHub cloud, ManagementService management)
    {
        var dispatcher = new McpBootDispatcher(projectId, workspace, files, clock, profiles, runs, integrations, management);
        var registry = new CapabilityRegistry();
        var probe = new BootFactsProbe(workspace, models: null, integrations, cloud, new HarnessStatusService(workspace, files, clock), profiles, modelDecidedByScreen: true);
        var boot = new BootService(workspace, registry, new IntentRecognizer(registry), probe, new BootPlanner(registry), store, clock, audit, context);
        boot.BindEngine(dispatcher.RoutedMethods, dispatcher.DispatchAsync);
        return boot;
    }
}
