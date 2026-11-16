using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;

namespace Mariliz.Application.Tests;

// H0-2 do estudo de 16/11: o sidecar e o MCP montavam a IaC e a gestão cada um do seu jeito, e o MCP ficou sem a política da
// edição (uma build comercial liberaria o Terraform gerenciado por ali) e sem o histórico de riscos.
public sealed class EngineCompositionTests : IDisposable
{
    private static readonly string[] OnlyInComposition =
        ["new IacRuntimeService(", "new IacOperationService(", "new IacStateService(", "new ManagementService("];

    private readonly TempProject _project = TempProject.Dotnet();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void A_politica_da_edicao_chega_a_iac_montada_pela_composicao()
    {
        using var composition = new EngineComposition(_project.DataDir, new FakeClock(), new MemoryAudit(), new SystemProcessRunner(),
            IacDistributionPolicy.CommercialDefault);
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem());

        var runtimes = composition.IacRuntimes(workspace);

        Assert.Same(IacDistributionPolicy.CommercialDefault, runtimes.Policy);
        Assert.False(runtimes.Policy.For(IacEngine.Terraform).ManagedInstall);
    }

    [Fact]
    public void Sidecar_e_mcp_so_montam_iac_e_gestao_pela_composicao_com_a_edicao_do_build()
    {
        var folder = Path.Combine(ContractFiles.Root, "runtime", "Mariliz.Runtime");
        var sources = Directory.GetFiles(folder, "*.cs")
            .ToDictionary(path => Path.GetFileName(path), File.ReadAllText, StringComparer.Ordinal);

        foreach (var entry in new[] { "Program.cs", "McpEntry.cs" })
        {
            Assert.Contains("new EngineComposition(", sources[entry]);
            Assert.Contains("BuildEdition.IacPolicy()", sources[entry]);
        }
        var offenders = sources
            .SelectMany(source => OnlyInComposition.Where(source.Value.Contains).Select(pattern => $"{source.Key}: {pattern}"))
            .Concat(sources.Where(source => source.Key != "BuildEdition.cs" && source.Value.Contains("IacDistributionPolicy.ForEdition("))
                .Select(source => $"{source.Key}: IacDistributionPolicy.ForEdition("))
            .ToList();
        Assert.True(offenders.Count == 0, "Montagem fora da EngineComposition: " + string.Join(", ", offenders));
    }
}
