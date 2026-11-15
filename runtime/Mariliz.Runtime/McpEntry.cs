using Mariliz.Application;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
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
        if (options.Scope is { } scope)
        {
            var known = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot)).Projects().Select(p => p.ProjectId).ToHashSet();
            foreach (var missing in scope.Where(id => !known.Contains(id)).Order(StringComparer.Ordinal))
                Console.Error.WriteLine($"Aviso: o projeto {missing} de --project não está aberto no Spock; as ferramentas vão recusá-lo até que esteja.");
        }
        // Um Python só deste processo, subido na primeira busca: o do aplicativo tem token próprio e pode nem estar aberto.
        var runtime = new LoopbackAiRuntime();
        var tools = new ProjectContextTools(
            () => new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot)),
            new LocalProjectFilesFactory(),
            clock,
            new FileTechnologyProfileStore(dataRoot),
            new FileDecisionStore(dataRoot),
            Environment.CurrentDirectory,
            runtime,
            dataRoot,
            new FileAuditLog(dataRoot, clock),
            options.Scope);
        try
        {
            await new McpServer(BuildInfo.Version, tools.Tools()).RunAsync(Console.In, Console.Out, ct);
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
