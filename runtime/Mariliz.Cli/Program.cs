using Mariliz.Application;
using Mariliz.Application.Cloud;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
using Mariliz.Cli;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

if (args is ["version"])
{
    Console.WriteLine(BuildInfo.Version);
    return 0;
}

using var cts = LocalEngine.ConfigureConsole();

// A pasta passada na linha de comando é a autorização do usuário, equivalente ao diálogo do aplicativo.
CliRunner? cli = null;
var engine = LocalEngine.Create(FileRunStore.DefaultDataRoot(), (runId, seq, type, data, at) => cli?.OnEvent(runId, seq, type, data, at), rememberWorkspaces: false);

cli = new CliRunner(engine.Configure, engine.Workspace, Console.In, Console.Out, Console.Error, interactive: !Console.IsInputRedirected,
    context: Context);
return await cli.RunAsync(args, cts.Token);

// Montado só para o comando context: só lê o que o aplicativo já gravou. A cloud entra sem provedores (listar vínculos e
// conexões não chama SDK nenhum), e o transporte das integrações nunca é usado.
ContextEngine Context()
{
    var audit = new FileAuditLog(engine.DataRoot, engine.Clock);
    return new ContextEngine(engine.Workspace, new HarnessStatusService(engine.Workspace, engine.Files, engine.Clock),
        new FileTechnologyProfileStore(engine.DataRoot), new TiktokenTokenizers(), engine.Clock,
        LocalIntegrationHub.Create(engine.DataRoot, engine.Workspace, engine.Clock, new HttpsConnectorTransport()),
        new CloudHub(new FileCloudStore(engine.DataRoot), new CloudProviderRegistry([]), engine.Workspace, engine.Clock, audit));
}
