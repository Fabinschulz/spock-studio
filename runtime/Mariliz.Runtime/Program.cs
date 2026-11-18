using Mariliz.Application;
using Mariliz.Application.AiTools;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Engineering;
using Mariliz.Application.Execution;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Management.Assistant;
using Mariliz.Application.Management.Investigation;
using Mariliz.Application.Mcp;
using Mariliz.Application.Audit;
using Mariliz.Application.Context;
using Mariliz.Application.Iac;
using Mariliz.Application.Ideas;
using Mariliz.Application.Security;
using Mariliz.Runtime;
using Mariliz.Application.Timeline;
using Mariliz.Application.Workflows;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;
using Mariliz.Infrastructure.Iac;

// stdout é exclusivo do protocolo; qualquer diagnóstico vai para stderr.
using var cts = LocalEngine.ConfigureConsole();

if (args is [IdeConnection.ServerArgument, ..]) return await McpEntry.RunAsync(args[1..], cts.Token);
if (args is ["version"])
{
    Console.WriteLine(BuildInfo.Version);
    return 0;
}
if (args is [ContextHookEntry.Command, ..]) return ContextHookEntry.Run(args[1..]);
// O host inicia o motor sem argumentos. Um argumento que não se entende (do lançador spock, por exemplo) não pode virar um
// sidecar parado esperando o protocolo na entrada.
if (args.Length > 0)
{
    Console.Error.WriteLine($"Argumento desconhecido: {args[0]}. Use: spock mcp --project-dir <pasta> | spock context session | spock context prompt | spock version");
    return 2;
}

var relay = new EventRelay();
var engine = LocalEngine.Create(FileRunStore.DefaultDataRoot(), relay.Emit, rememberWorkspaces: true);
using var transport = new HttpsModelTransport();
var models = new ModelGateway(engine.Settings, transport, chat: transport, checks: new FileModelCheckStore(engine.DataRoot), clock: engine.Clock,
    preferences: new FileModelPreferences(engine.DataRoot), budgets: new FileModelBudgets(engine.DataRoot));
using var modelAccess = new LocalModelAccess(new ModelBroker(models));
var aiRuntime = new LoopbackAiRuntime();
// Antes dos workflows: o que um projeto autorizou como contexto entra no índice do workflow.
using var connectorTransport = new HttpsConnectorTransport();
var integrations = LocalIntegrationHub.Create(engine.DataRoot, engine.Workspace, engine.Clock, connectorTransport, relay.Emit);
// Antes dos workflows, que montam o contexto com as decisões como memória do índice.
var decisionStore = new FileDecisionStore(engine.DataRoot);
var decisionMemory = new DecisionMemory(engine.Workspace, decisionStore);
var workflowContext = new WorkflowContextBuilder(engine.Workspace, aiRuntime, engine.Files, engine.Runner, engine.DataRoot, integrations.ExternalKnowledge, decisionMemory);
var workflows = new WorkflowService(engine.Workspace, aiRuntime, engine.Tracker, engine.Clock, engine.Store, workflowContext, modelAccess,
    new FileEvalRecords(engine.DataRoot), new ProjectActionExecutor(engine.Workspace, engine.Files));
// Uma execução que a queda anterior deixou "em andamento" não vai terminar: vira falha antes de qualquer pedido.
if (WorkflowRecovery.MarkInterrupted(engine.Store, engine.Clock) is > 0 and var interrupted)
    await Console.Error.WriteLineAsync($"{interrupted} execução(ões) interrompida(s) pela queda anterior foram marcadas como falha.");
using var composition = new EngineComposition(engine.DataRoot, engine.Clock, integrations.Audit, engine.Runner, BuildEdition.IacPolicy());
var management = composition.Management(engine.Workspace, integrations);
integrations.Sync.Finished += (integration, run) =>
{
    if (run.Status is SyncStatus.Succeeded) management.CaptureRisks(integration.ProjectId);
};
var runs = new RunQueries(engine.Tracker, engine.Store,
    new RunCatalog(engine.Store, engine.Workspace, engine.Tracker, new RunLinkService(engine.Store, engine.Store, integrations, engine.Workspace, engine.Clock)));
var assistant = new ManagementAssistant(management, integrations, engine.Workspace, models, engine.Clock, engine.Store);
var decisions = new DecisionService(engine.Workspace, decisionStore, engine.Store, assistant.Evidence, integrations.Audit, engine.Clock);
var profiles = new TechnologyProfileService(engine.Configure, integrations, engine.Workspace, new FileTechnologyProfileStore(engine.DataRoot), engine.Clock);
var harnessRevisions = new HarnessRevisionService(engine.Workspace, engine.Files, engine.Store);
var timeline = new ProjectTimelineService(engine.Workspace, engine.Clock, engine.Store, harnessRevisions, profiles, decisions, management, integrations);
var audit = new ProjectAuditService(engine.Workspace, engine.Clock, engine.Store, integrations.AuditReader, integrations);
var investigation = new InvestigationService(assistant, integrations, engine.Workspace, aiRuntime, engine.Clock, engine.Store, modelAccess, timeline,
    decisionMemory, Path.Combine(engine.DataRoot, "investigation-index"));
var portfolioAssistant = new PortfolioAssistant(assistant, engine.Workspace, models, integrations, engine.Clock);
// Credenciais da cloud ficam onde o SDK de cada provedor as procura; a trilha é a mesma das integrações.
var cloud = LocalCloudHub.Create(engine.DataRoot, engine.Workspace, engine.Clock, integrations.Audit);
var iacRuntimes = composition.IacRuntimes(engine.Workspace);
var knowledge = new ProjectKnowledgeService(engine.Workspace, engine.Files, aiRuntime, engine.DataRoot, engine.Clock, integrations.ExternalKnowledge, decisionMemory);
var iacOperations = composition.IacOperations(iacRuntimes, engine.Workspace, cloud);
var iacSchedules = new IacDriftScheduler(iacOperations, new FileIacDriftScheduleStore(engine.DataRoot), engine.Workspace, engine.Clock, integrations.Audit);
var session = new EngineSession(BuildInfo.Version, workspace: engine.Workspace, configure: engine.Configure, relay: relay,
    tracker: engine.Tracker, workflows: workflows, models: models, runs: runs, integrations: integrations,
    management: management, assistant: assistant,
    decisions: decisions, reports: new ReportService(engine.Workspace, management, decisions, engine.Store, engine.Clock),
    profiles: profiles,
    knowledge: knowledge,
    harness: new HarnessStatusService(engine.Workspace, engine.Files, engine.Clock),
    revisions: harnessRevisions,
    mcp: new McpInventoryService(engine.Workspace, engine.Files), timeline: timeline, audit: audit, investigation: investigation, portfolioAssistant: portfolioAssistant,
    aiTools: new AiToolService(ExecutableLocator.FromEnvironment(), engine.Runner),
    agents: new AgentRunService(ExecutableLocator.FromEnvironment(), engine.Runner, engine.Workspace, engine.Tracker, engine.Store, engine.Clock),
    ide: new IdeConnection(Environment.ProcessPath, () => LauncherLocator.IsInstalled(Environment.ProcessPath)),
    contextReview: new ContextReviewService(engine.Workspace, workflowContext, aiRuntime, new TiktokenTokenizers(), models, knowledge),
    security: new SecurityService(engine.Workspace, engine.Files, engine.Store, engine.Clock, engine.Tracker),
    ideas: new IdeaArchitectService(engine.Workspace, models, engine.Files, engine.Store, engine.Clock, engine.Tracker),
    cloud: cloud,
    terraform: iacRuntimes,
    iacOperations: iacOperations,
    iacState: composition.IacState(engine.Workspace, cloud, engine.Files, engine.Store, engine.Tracker),
    iacGeneration: new IacGenerationService(engine.Workspace, new IacConfigReader(), models, engine.Files, engine.Store, engine.Clock, engine.Tracker,
        integrations.Audit),
    iacCosts: new IacCostService(engine.Workspace, cloud, engine.Clock, iacOperations),
    iacSchedules: iacSchedules);
// Verificação de drift agendada: uma volta por minuto enquanto o motor vive; falha de uma volta não derruba o laço.
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    try
    {
        while (await timer.WaitForNextTickAsync(cts.Token))
        {
            try
            {
                await iacSchedules.RunDueAsync(cts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine("Drift agendado: " + ex.GetType().Name);
            }
        }
    }
    catch (OperationCanceledException)
    {
    }
});
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
