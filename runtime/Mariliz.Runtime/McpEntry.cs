using Mariliz.Application;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// Modo iniciado pela IDE: só lê o que o aplicativo já gravou e nunca autoriza pasta nova.
internal static class McpEntry
{
    public static async Task<int> RunAsync(CancellationToken ct)
    {
        var dataRoot = FileRunStore.DefaultDataRoot();
        var tools = new ProjectContextTools(
            () => new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot)),
            new LocalProjectFilesFactory(),
            new SystemClock(),
            new FileTechnologyProfileStore(dataRoot),
            new FileDecisionStore(dataRoot),
            Environment.CurrentDirectory);
        try
        {
            await new McpServer(BuildInfo.Version, tools.Tools()).RunAsync(Console.In, Console.Out, ct);
        }
        catch (OperationCanceledException)
        {
        }
        return 0;
    }
}
