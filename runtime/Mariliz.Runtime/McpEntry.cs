using Mariliz.Application;
using Mariliz.Application.Iac;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Engineering;
using Mariliz.Infrastructure.Integrations;
using Mariliz.Infrastructure.Terraform;

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
        Func<WorkspaceAuthorizer> workspaces = () => new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(dataRoot));
        var audit = new FileAuditLog(dataRoot, clock);
        var cloud = new CloudMcpTools(LocalCloudHub.Create(dataRoot, workspaces(), clock, audit), workspaces, Environment.CurrentDirectory, options.Scope);
        // Só a consulta: o servidor MCP nunca baixa nem instala, e o binário externo só é perguntado pela versão.
        using var terraformToolchain = new HashiCorpTerraformToolchain(dataRoot, ExecutableLocator.FromEnvironment());
        var iac = new IacMcpTools(workspaces, authorizer => new TerraformDependencyService(terraformToolchain, new FileTerraformSettingsStore(dataRoot),
            new TerraformConfigReader(), new SystemProcessRunner(), authorizer, clock, audit), Environment.CurrentDirectory, options.Scope);
        var tools = new ProjectContextTools(
            workspaces,
            new LocalProjectFilesFactory(),
            clock,
            new FileTechnologyProfileStore(dataRoot),
            new FileDecisionStore(dataRoot),
            Environment.CurrentDirectory,
            runtime,
            dataRoot,
            audit,
            options.Scope);
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
