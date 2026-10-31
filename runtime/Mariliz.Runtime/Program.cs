using System.Text;
using Mariliz.Application;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workflows;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

// stdout é exclusivo do protocolo; qualquer diagnóstico vai para stderr.
var utf8 = new UTF8Encoding(false);
Console.InputEncoding = utf8;
Console.OutputEncoding = utf8;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
var relay = new EventRelay();
var tracker = new RunTracker(relay.Emit);
var clock = new SystemClock();
var runner = new SystemProcessRunner();
var dataRoot = FileRunStore.DefaultDataRoot();
var configure = new ConfigureProjectService(
    workspace, new LocalProjectFilesFactory(), runner, new FileRunStore(dataRoot), clock, tracker);
var workflows = new WorkflowService(workspace, new LoopbackAiRuntime(), tracker, clock, new LocalProjectFilesFactory(), runner, dataRoot);
var session = new EngineSession(BuildInfo.Version, workspace: workspace, configure: configure, relay: relay, tracker: tracker, workflows: workflows);
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
