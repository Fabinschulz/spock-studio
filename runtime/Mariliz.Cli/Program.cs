using System.Text;
using Mariliz.Application;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Cli;
using Mariliz.Infrastructure;

if (args is ["version"])
{
    Console.WriteLine(BuildInfo.Version);
    return 0;
}

var utf8 = new UTF8Encoding(false);
Console.InputEncoding = utf8;
Console.OutputEncoding = utf8;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// A pasta passada na linha de comando é a autorização do usuário, equivalente ao diálogo do aplicativo.
var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());
CliRunner? cli = null;
var tracker = new RunTracker((runId, seq, type, data) => cli?.OnEvent(runId, seq, type, data));
var service = new ConfigureProjectService(
    workspace, new LocalProjectFilesFactory(), new SystemProcessRunner(), new FileRunStore(FileRunStore.DefaultDataRoot()), new SystemClock(), tracker);

cli = new CliRunner(service, workspace, Console.In, Console.Out, Console.Error, interactive: !Console.IsInputRedirected);
return await cli.RunAsync(args, cts.Token);
