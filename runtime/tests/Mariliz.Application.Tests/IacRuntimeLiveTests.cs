using Mariliz.Application.Iac;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Iac;
using Xunit.Abstractions;

namespace Mariliz.Application.Tests;

// Instalação de verdade: baixa da fonte oficial (GitHub do OpenTofu, releases.hashicorp.com), confere a assinatura com a
// chave embutida e roda o binário instalado. Só com MARILIZ_LIVE_TERRAFORM=1 no terminal: baixa de 40 a 60 MB por
// ferramenta para uma pasta temporária, que é apagada no fim.
public sealed class IacRuntimeLiveTests(ITestOutputHelper output)
{
    public const string Variable = "MARILIZ_LIVE_TERRAFORM";

    [LiveFact(Variable)]
    public Task Instala_o_OpenTofu_publicado_e_o_binario_responde() => InstallAndRun(IacEngine.OpenTofu);

    [LiveFact(Variable)]
    public Task Instala_o_Terraform_publicado_e_o_binario_responde() => InstallAndRun(IacEngine.Terraform);

    private async Task InstallAndRun(IacEngine engine)
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        using var terraform = new HashiCorpTerraformToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var service = new IacRuntimeService([tofu, terraform], new FileIacSettingsStore(project.DataDir), new IacConfigReader(),
            runner, workspace, new FakeClock(), new MemoryAudit());

        var offer = await service.OfferAsync(null, null, engine, CancellationToken.None);
        var status = await service.InstallAsync(offer.OfferId, offer.Version, engine, CancellationToken.None);
        await service.PinAsync(projectId, offer.Version, IacRuntimeSource.Managed, engine, CancellationToken.None);
        var used = await service.ResolveForUseAsync(projectId, CancellationToken.None);
        var version = await runner.RunAsync(new Application.Ports.ProcessSpec(used.Path, ["version", "-json"], project.Root,
            TimeSpan.FromSeconds(30), Environment: IacRuntimeService.QuietEnvironment), CancellationToken.None);

        var signedBy = (engine == IacEngine.OpenTofu ? (OfficialReleaseToolchain)tofu : terraform).Managed().Single().SignedBy;
        output.WriteLine($"Instalado {offer.EngineName} {offer.Version} para {offer.Platform.Key}, assinado pela chave {signedBy}");
        output.WriteLine(version.StandardOutput);
        Assert.Equal(IacRuntimeState.Ready, status.State);
        Assert.Equal(engine, used.Engine);
        Assert.Equal(0, version.ExitCode);
        Assert.Contains($"\"{offer.Version}\"", version.StandardOutput);
    }

    private sealed class NoExternal : Application.Ports.IExecutableLocator
    {
        public string? Find(string name) => null;
    }
}
