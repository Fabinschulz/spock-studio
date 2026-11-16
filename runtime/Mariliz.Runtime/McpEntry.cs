using Mariliz.Application;
using Mariliz.Application.Execution;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// Modo iniciado pela IDE: só lê o que o aplicativo já gravou, anota cada leitura na trilha de auditoria e nunca autoriza pasta nova.
internal static class McpEntry
{
    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        McpOptions options;
        try
        {
            options = McpOptions.Parse(args);
        }
        catch (McpOptionsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var dataRoot = FileRunStore.DefaultDataRoot();
        var clock = new SystemClock();
        // Não derruba: o projeto pode ser aberto no Spock depois, e o registro é relido a cada chamada.
        var known = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot)).Projects().Select(p => p.ProjectId).ToHashSet();
        foreach (var missing in options.Scope.Where(id => !known.Contains(id)).Order(StringComparer.Ordinal))
            Console.Error.WriteLine($"Aviso: o projeto {missing} de --project não está aberto no Spock; as ferramentas vão recusá-lo até que esteja.");
        // Um Python só deste processo, subido na primeira busca: o do aplicativo tem token próprio e pode nem estar aberto.
        var runtime = new LoopbackAiRuntime();
        Func<WorkspaceAuthorizer> workspaces = () => new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
        var audit = new FileAuditLog(dataRoot, clock);
        using var composition = new EngineComposition(dataRoot, clock, audit, new SystemProcessRunner(), BuildEdition.IacPolicy());
        // A gestão só lê o que o aplicativo já sincronizou; o transporte existe porque o hub de integrações o exige, e aqui nunca é usado.
        using var connectorTransport = new HttpsConnectorTransport();
        var cloud = new CloudMcpTools(LocalCloudHub.Create(dataRoot, workspaces(), clock, audit), workspaces, Environment.CurrentDirectory, options.Scope);
        // O servidor MCP nunca instala a ferramenta nem planeja: consulta, valida sobre uma cópia e lê planos já gerados. Por isso a
        // IaC daqui não recebe a cloud: nada do que ela expõe lê a conta, e sem a cloud nenhum caminho futuro passa a ler por engano.
        var iac = new IacMcpTools(workspaces, composition.IacRuntimes,
            authorizer => composition.IacOperations(composition.IacRuntimes(authorizer), authorizer, cloud: null),
            Environment.CurrentDirectory, options.Scope,
            authorizer => composition.IacState(authorizer, cloud: null, new LocalProjectFilesFactory(), new FileRunStore(dataRoot), new RunTracker()));
        var tools = new ProjectContextTools(
            workspaces,
            new LocalProjectFilesFactory(),
            clock,
            new FileTechnologyProfileStore(dataRoot),
            new FileDecisionStore(dataRoot),
            Environment.CurrentDirectory,
            options.Scope,
            runtime,
            dataRoot,
            audit,
            workspace => composition.Management(workspace, LocalIntegrationHub.Create(dataRoot, workspace, clock, connectorTransport)));
        try
        {
            await new McpServer(BuildInfo.Version, [.. tools.Tools(), .. cloud.Tools(), .. iac.Tools()]).RunAsync(Console.In, Console.Out, ct);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await runtime.ShutdownAsync();
        }
        return 0;
    }
}
