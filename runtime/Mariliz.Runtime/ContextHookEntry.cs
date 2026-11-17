using System.Text.Json.Nodes;
using Mariliz.Application.Context;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Runtime;

// C2: o que o hook de início de sessão da IDE chama ("spock context session"). A saída padrão vira contexto da sessão, então
// só sai por ali o pacote. Nunca falha a sessão: pasta fora do Spock ou erro viram uma linha de aviso e código 0.
internal static class ContextHookEntry
{
    public const string Command = "context";
    public const string AuditAction = "ide.bootstrap";

    public const string Usage = """
        Uso: spock context session [--project-dir <pasta>] [--budget <tokens>]

        Escreve na saída padrão o contexto de abertura do projeto da pasta (padrão: CLAUDE_PROJECT_DIR ou a pasta atual),
        para o hook de início de sessão da IDE. Só lê o que o Spock Studio já gravou.
        """;

    public static int Run(string[] args)
    {
        if (args is not ["session", .. var options]) return UsageError("Use 'context session'.");
        string? directory = null;
        int? budget = null;
        for (var index = 0; index < options.Length; index++)
        {
            var value = index + 1 < options.Length ? options[index + 1] : null;
            switch (options[index])
            {
                case "--project-dir" when !string.IsNullOrWhiteSpace(value):
                    directory = value;
                    break;
                case "--budget" when int.TryParse(value, out var tokens):
                    budget = tokens;
                    break;
                default:
                    return UsageError($"Argumento desconhecido ou sem valor: {options[index]}.");
            }
            index++;
        }

        try
        {
            Console.Out.Write(Compose(directory ?? Environment.GetEnvironmentVariable("CLAUDE_PROJECT_DIR") ?? Environment.CurrentDirectory, budget));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // O código 0 deixa a sessão abrir; a mensagem vai para a IDE em vez do contexto.
            Console.Out.WriteLine($"Spock: o contexto do projeto não pôde ser montado agora ({ex.GetType().Name}). Chame spock_context_bootstrap pelo MCP.");
        }
        return 0;
    }

    private static string Compose(string directory, int? budget)
    {
        var dataRoot = FileRunStore.DefaultDataRoot();
        var clock = new SystemClock();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
        var all = workspace.Projects().Select(project => project.ProjectId).ToHashSet(StringComparer.Ordinal);
        // A pasta da IDE escolhe a cópia, como no MCP com --project-dir; nunca autoriza pasta nova.
        if (new McpProjectResolver(workspace, Path.GetFullPath(directory), all).Current() is not { } project)
            return "Spock: esta pasta não está aberta no Spock Studio. Abra-a no aplicativo para a IDE receber o contexto do projeto.\n";

        var audit = new FileAuditLog(dataRoot, clock);
        using var transport = new HttpsConnectorTransport();
        var package = ContextComposition.Engine(workspace, dataRoot, clock, audit, transport, new TiktokenTokenizers())
            .Compose(project.ProjectId, ContextIntents.Bootstrap, budget);
        audit.Record(AuditAction, new JsonObject
        {
            ["projectId"] = project.ProjectId,
            ["contextVersion"] = package.ContextVersion,
            ["items"] = package.Items.Count,
            ["usedTokens"] = package.UsedTokens
        });
        return package.Text;
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
