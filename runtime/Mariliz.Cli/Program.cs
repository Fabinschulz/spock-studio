using Mariliz.Application;
using Mariliz.Cli;
using Mariliz.Infrastructure;

if (args is ["version"])
{
    Console.WriteLine(BuildInfo.Version);
    return 0;
}

using var cts = LocalEngine.ConfigureConsole();

// A pasta passada na linha de comando é a autorização do usuário, equivalente ao diálogo do aplicativo.
CliRunner? cli = null;
var engine = LocalEngine.Create(FileRunStore.DefaultDataRoot(), (runId, seq, type, data) => cli?.OnEvent(runId, seq, type, data), rememberWorkspaces: false);

cli = new CliRunner(engine.Configure, engine.Workspace, Console.In, Console.Out, Console.Error, interactive: !Console.IsInputRedirected);
return await cli.RunAsync(args, cts.Token);
