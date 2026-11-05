using Mariliz.Application;
using Mariliz.Application.Models;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workflows;
using Mariliz.Infrastructure;

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
var session = new EngineSession(BuildInfo.Version, workspace: engine.Workspace, configure: engine.Configure, relay: relay,
    tracker: engine.Tracker, workflows: workflows, models: models, runs: engine.Runs);
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
