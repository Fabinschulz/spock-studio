using Mariliz.Application;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Engineering;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Workflows;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

// stdout é exclusivo do protocolo; qualquer diagnóstico vai para stderr.
using var cts = LocalEngine.ConfigureConsole();

var relay = new EventRelay();
var engine = LocalEngine.Create(FileRunStore.DefaultDataRoot(), relay.Emit, rememberWorkspaces: true);
using var transport = new HttpsModelTransport();
var models = new ModelGateway(engine.Settings, transport, chat: transport);
using var modelAccess = new LocalModelAccess(new ModelBroker(models));
var aiRuntime = new LoopbackAiRuntime();
var workflows = new WorkflowService(engine.Workspace, aiRuntime, engine.Tracker, engine.Clock, engine.Store,
    new WorkflowContextBuilder(engine.Workspace, aiRuntime, engine.Files, engine.Runner, engine.DataRoot), modelAccess,
    new FileEvalRecords(engine.DataRoot), new ProjectActionExecutor(engine.Workspace, engine.Files));
// Uma execução que a queda anterior deixou "em andamento" não vai terminar: vira falha antes de qualquer pedido.
if (WorkflowRecovery.MarkInterrupted(engine.Store, engine.Clock) is > 0 and var interrupted)
    await Console.Error.WriteLineAsync($"{interrupted} execução(ões) interrompida(s) pela queda anterior foram marcadas como falha.");
using var connectorTransport = new HttpsConnectorTransport();
var integrations = LocalIntegrationHub.Create(engine.DataRoot, engine.Workspace, engine.Clock, connectorTransport);
var management = new ManagementService(engine.Workspace, integrations, engine.Clock);
var session = new EngineSession(BuildInfo.Version, workspace: engine.Workspace, configure: engine.Configure, relay: relay,
    tracker: engine.Tracker, workflows: workflows, models: models, runs: engine.Runs, integrations: integrations,
    management: management, assistant: new ManagementAssistant(management, integrations, engine.Workspace, models, engine.Clock),
    profiles: new TechnologyProfileService(engine.Configure, integrations, engine.Workspace, new FileTechnologyProfileStore(engine.DataRoot), engine.Clock),
    knowledge: new ProjectKnowledgeService(engine.Workspace, engine.Files, aiRuntime, engine.DataRoot, engine.Clock),
    harness: new HarnessStatusService(engine.Workspace, engine.Files, engine.Clock),
    revisions: new HarnessRevisionService(engine.Workspace, engine.Files, engine.Store));
try
{
    await session.RunAsync(Console.In, Console.Out, cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    return 0;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Falha inesperada no motor: {ex.GetType().Name}");
    return 1;
}
