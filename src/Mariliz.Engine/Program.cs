using System.Text;
using Mariliz.Application;
using Mariliz.Application.Protocol;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

// stdout é exclusivo do protocolo; qualquer diagnóstico vai para stderr.
var utf8 = new UTF8Encoding(false);
Console.InputEncoding = utf8;
Console.OutputEncoding = utf8;

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var session = new EngineSession(BuildInfo.Version, workspace: new WorkspaceAuthorizer(new LocalWorkspaceFileSystem()));
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
