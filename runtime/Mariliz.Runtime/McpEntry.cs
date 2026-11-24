using Mariliz.Application;
using Mariliz.Application.Context;
using Mariliz.Application.Engineering;
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
            options = McpOptions.Parse(args).WithDirectoryResolved(DeclarationIn);
        }
        catch (McpOptionsException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        var dataRoot = FileRunStore.DefaultDataRoot();
        var clock = new SystemClock();
        // Não derruba: o projeto pode ser aberto no Spock depois, e o registro é relido a cada chamada.
        var startup = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
        var known = startup.Projects().SelectMany(p => new[] { p.ProjectId, startup.StableIdOf(p.ProjectId) }).OfType<string>().ToHashSet();
        foreach (var missing in options.Scope.Where(id => !known.Contains(id)).Order(StringComparer.Ordinal))
            Console.Error.WriteLine($"Aviso: o projeto {missing} não está aberto no Spock nesta máquina; as ferramentas vão recusá-lo até que esteja.");
        // Com --project-dir, é a pasta da IDE que escolhe a cópia, mesmo que o processo tenha sido iniciado em outra.
        var workingDirectory = options.ProjectDirectory ?? Environment.CurrentDirectory;
        // Um Python só deste processo, subido na primeira busca: o do aplicativo tem token próprio e pode nem estar aberto.
        var runtime = new LoopbackAiRuntime();
        Func<WorkspaceAuthorizer> workspaces = () => new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
        var audit = new FileAuditLog(dataRoot, clock);
        using var composition = new EngineComposition(dataRoot, clock, audit, new SystemProcessRunner(), BuildEdition.IacPolicy());
        // A gestão só lê o que o aplicativo já sincronizou; o transporte existe porque o hub de integrações o exige, e aqui nunca é usado.
        using var connectorTransport = new HttpsConnectorTransport();
        var cloud = new CloudMcpTools(LocalCloudHub.Create(dataRoot, workspaces(), clock, audit), workspaces, workingDirectory, options.Scope);
        // O servidor MCP nunca instala a ferramenta nem planeja: consulta, valida sobre uma cópia e lê planos já gerados. Por isso a
        // IaC daqui não recebe a cloud: nada do que ela expõe lê a conta, e sem a cloud nenhum caminho futuro passa a ler por engano.
        var iac = new IacMcpTools(workspaces, composition.IacRuntimes,
            authorizer => composition.IacOperations(composition.IacRuntimes(authorizer), authorizer, cloud: null),
            workingDirectory, options.Scope,
            authorizer => composition.IacState(authorizer, cloud: null, new LocalProjectFilesFactory(), new FileRunStore(dataRoot), new RunTracker()),
            new LocalProjectFilesFactory());
        var tokenizers = new TiktokenTokenizers();
        var tools = new ProjectContextTools(
            workspaces,
            new LocalProjectFilesFactory(),
            clock,
            new FileTechnologyProfileStore(dataRoot),
            new FileDecisionStore(dataRoot),
            workingDirectory,
            options.Scope,
            runtime,
            dataRoot,
            audit,
            workspace => composition.Management(workspace, LocalIntegrationHub.Create(dataRoot, workspace, clock, connectorTransport)),
            workspace => ContextComposition.Engine(workspace, dataRoot, clock, audit, connectorTransport, tokenizers));
        try
        {
            var assessment = new ArchitectureAssessmentTools(workspaces, new LocalProjectFilesFactory(), workingDirectory, options.Scope, audit);
            var modernization = new ModernizationPlanTools(workspaces, new LocalProjectFilesFactory(), workingDirectory, options.Scope,
                new FileModernizationApprovalStore(dataRoot), audit);
            await new McpServer(BuildInfo.Version, [.. tools.Tools(), .. assessment.Tools(), .. modernization.Tools(), .. cloud.Tools(), .. iac.Tools()])
                .RunStdioAsync(ct);
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

    private static string? DeclarationIn(string directory)
    {
        // Na mesma ordem da detecção do projeto: spock.yaml, depois o mariliz.yaml de um projeto anterior à troca de nome.
        foreach (var naming in Core.Policy.HarnessNaming.All)
        {
            var file = new FileInfo(Path.Combine(directory, naming.Declaration));
            try
            {
                if (file.Exists && file.Length <= 256 * 1024) return File.ReadAllText(file.FullName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        return null;
    }
}
