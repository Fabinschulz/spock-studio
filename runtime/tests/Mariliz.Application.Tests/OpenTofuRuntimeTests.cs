using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// O OpenTofu é o padrão: gratuito (MPL 2.0). Servidores falsos no formato do get.opentofu.org e das releases do GitHub,
// com o redirecionamento para a CDN; a instalação real está em IacRuntimeLiveTests.
public sealed class OpenTofuRuntimeTests : IDisposable
{
    private static readonly IacPlatform Windows = new("windows", "amd64");

    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly TestPgp _tofuKey = new();
    private readonly TestPgp _hashiKey = new();
    private readonly FakeOpenTofu _tofu = new();
    private readonly FakeHashiCorp _hashi = new();
    private readonly string _projectId;

    public OpenTofuRuntimeTests() => _projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;

    public void Dispose()
    {
        _tofuKey.Dispose();
        _hashiKey.Dispose();
        _project.Dispose();
    }

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private IacRuntimeService Service() =>
        new([new OpenTofuToolchain(_project.DataDir, new NoExternal(), _tofu, Windows, _tofuKey.Keyring()),
                new HashiCorpTerraformToolchain(_project.DataDir, new NoExternal(), _hashi, Windows, _hashiKey.Keyring())],
            new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), new InterceptingRunner(), Workspace(), _clock, _audit);

    private void PublishTofu(string version) => _tofu.Publish(version, Windows.Key, FakeHashiCorp.Zip(("tofu.exe", $"tofu {version}"), ("LICENSE", "MPL")), _tofuKey);

    private void PublishTerraform(string version) => _hashi.Publish(version, Windows.Key, FakeHashiCorp.Zip(("terraform.exe", $"terraform {version}")), _hashiKey);

    [Fact]
    public void Assinatura_real_do_OpenTofu_e_aceita_com_a_chave_embutida()
    {
        var keyring = OpenPgpKeyring.FromArmored(File.ReadAllText(Path.Combine(ContractFiles.Root,
            "runtime", "Mariliz.Infrastructure", "Iac", "opentofu-release-key.asc")));
        var fixtures = Path.Combine(ContractFiles.Root, "runtime", "tests", "Mariliz.Application.Tests", "Fixtures", "iac");
        var sums = File.ReadAllBytes(Path.Combine(fixtures, "tofu_1.13.9_SHA256SUMS"));
        var signature = File.ReadAllBytes(Path.Combine(fixtures, "tofu_1.13.9_SHA256SUMS.gpgsig"));

        Assert.Equal("E3E6E43D84CB852EADB0051D0C0AF313E5FD9F80", keyring.PrimaryFingerprint);
        Assert.Equal("E3E6E43D84CB852EADB0051D0C0AF313E5FD9F80", keyring.Verify(sums, signature));
        sums[10] ^= 0x01;
        Assert.Null(keyring.Verify(sums, signature));
    }

    [Fact]
    public async Task Sem_escolha_a_proposta_e_do_OpenTofu_gratuito()
    {
        PublishTofu("1.13.9");
        PublishTerraform("1.17.2");

        var offer = await Service().OfferAsync(null, null, null, CancellationToken.None);
        var status = await Service().StatusAsync(null, CancellationToken.None);

        Assert.Equal(IacEngine.OpenTofu, offer.Engine);
        Assert.Equal("1.13.9", offer.Version);
        Assert.Contains("MPL 2.0", offer.License);
        Assert.Contains(offer.Impacts, i => i.Contains("Gratuito e de código aberto"));
        Assert.Equal(_tofuKey.Fingerprint, offer.SigningKey);
        Assert.Empty(_hashi.Requested);
        Assert.True(status.Engines.Single(e => e.Engine == IacEngine.OpenTofu).Preferred);
        Assert.Contains("OpenTofu, gratuito", status.Message);
    }

    [Fact]
    public async Task Instala_o_OpenTofu_seguindo_so_o_redirecionamento_para_a_CDN_do_GitHub()
    {
        PublishTofu("1.13.9");
        var service = Service();
        var offer = await service.OfferAsync(null, null, null, CancellationToken.None);

        var status = await service.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);

        var installed = Assert.Single(status.Installations);
        Assert.Equal(IacEngine.OpenTofu, installed.Engine);
        Assert.Equal(Path.Combine(_project.DataDir, "tools", "opentofu", "1.13.9", "tofu.exe"), installed.Path);
        Assert.Equal("tofu 1.13.9", File.ReadAllText(installed.Path));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(installed.Path)!, "LICENSE")));
        Assert.Contains(_tofu.Requested, url => url.StartsWith(FakeOpenTofu.Cdn, StringComparison.Ordinal));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.installed" && e.Detail.Contains("\"engine\":\"openTofu\""));
    }

    [Fact]
    public async Task Redirecionamento_para_fora_da_CDN_do_GitHub_e_recusado()
    {
        PublishTofu("1.13.9");
        var service = Service();
        var offer = await service.OfferAsync(null, null, null, CancellationToken.None);
        _tofu.RedirectBase = "https://espelho.exemplo.com/";

        var error = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None));

        Assert.Equal("IAC_DOWNLOAD_FAILED", error.Code);
        Assert.DoesNotContain(_tofu.Requested, url => url.StartsWith("https://espelho", StringComparison.Ordinal));
        Assert.Empty((await service.StatusAsync(null, CancellationToken.None)).Installations);
    }

    [Fact]
    public async Task Com_as_duas_instaladas_o_projeto_usa_o_OpenTofu_e_pode_fixar_o_Terraform()
    {
        PublishTofu("1.13.9");
        PublishTerraform("1.17.2");
        var service = Service();
        foreach (var engine in new[] { IacEngine.Terraform, IacEngine.OpenTofu })
        {
            var offer = await service.OfferAsync(null, null, engine, CancellationToken.None);
            await service.InstallAsync(offer.OfferId, offer.Version, engine, CancellationToken.None);
        }

        var automatic = await service.StatusAsync(_projectId, CancellationToken.None);
        var pinned = await service.PinAsync(_projectId, "1.17.2", IacRuntimeSource.Managed, IacEngine.Terraform, CancellationToken.None);
        var wrongEngine = await Assert.ThrowsAsync<EngineException>(() =>
            service.PinAsync(_projectId, "1.17.2", null, IacEngine.OpenTofu, CancellationToken.None));

        Assert.Equal(IacEngine.OpenTofu, automatic.Project!.Selected!.Engine);
        Assert.True(automatic.Project.Automatic);
        Assert.Equal(IacEngine.Terraform, pinned.Project!.Selected!.Engine);
        Assert.Equal(IacEngine.Terraform, pinned.Project.Pin!.Engine);
        Assert.Equal("IAC_TERRAFORM_UNAVAILABLE", wrongEngine.Code);
    }

    [Fact]
    public async Task Projeto_com_required_version_do_modelo_aceita_o_OpenTofu_atual()
    {
        _project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.11\"\n}\n");
        _project.Write("infra/override.tofu", "terraform {\n  required_version = \"< 2.0\"\n}\n");
        PublishTofu("1.13.9");

        var offer = await Service().OfferAsync(null, _projectId, null, CancellationToken.None);
        var status = await Service().StatusAsync(_projectId, CancellationToken.None);

        Assert.Equal("1.13.9", offer.Version);
        Assert.Equal(2, status.Project!.Requirements.Count);
        Assert.DoesNotContain(offer.Impacts, i => i.Contains("não poderá ser fixada"));
    }

    private sealed class NoExternal : IExecutableLocator
    {
        public string? Find(string name) => null;
    }
}
