using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Terraform;
using Xunit.Abstractions;

namespace Mariliz.Application.Tests;

// Instalação de verdade: baixa da HashiCorp, confere a assinatura com a chave embutida e roda o binário instalado.
// Só com MARILIZ_LIVE_TERRAFORM=1 no terminal: baixa cerca de 40 MB para uma pasta temporária, que é apagada no fim.
public sealed class TerraformLiveTests(ITestOutputHelper output)
{
    public const string Variable = "MARILIZ_LIVE_TERRAFORM";

    [LiveFact(Variable)]
    public async Task Instala_a_versao_publicada_e_o_binario_responde()
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var toolchain = new HashiCorpTerraformToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var service = new TerraformDependencyService(toolchain, new FileTerraformSettingsStore(project.DataDir), new TerraformConfigReader(),
            runner, workspace, new FakeClock(), new MemoryAudit());

        var offer = await service.OfferAsync(null, null, CancellationToken.None);
        var status = await service.InstallAsync(offer.OfferId, offer.Version, CancellationToken.None);
        var used = await service.ResolveForUseAsync(projectId, CancellationToken.None);
        var version = await runner.RunAsync(new Application.Ports.ProcessSpec(used.Path, ["version", "-json"], project.Root,
            TimeSpan.FromSeconds(30), Environment: TerraformDependencyService.QuietEnvironment), CancellationToken.None);

        output.WriteLine($"Instalado {offer.Version} para {offer.Platform.Key}, assinado pela chave {toolchain.Managed().Single().SignedBy}");
        output.WriteLine(version.StandardOutput);
        Assert.Equal(TerraformState.Ready, status.State);
        Assert.Equal(0, version.ExitCode);
        Assert.Contains($"\"terraform_version\": \"{offer.Version}\"", version.StandardOutput);
    }

    private sealed class NoExternal : Application.Ports.IExecutableLocator
    {
        public string? Find(string name) => null;
    }
}
