using System.Text;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Mcp;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// A dependência Terraform sem rede de verdade: um servidor falso no formato da HashiCorp, uma chave de teste e um
// Terraform "externo" simulado pelo executor de processos. A instalação real está em IacRuntimeLiveTests.
public sealed class IacRuntimeTests : IDisposable
{
    private static readonly IacPlatform Windows = new("windows", "amd64");

    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly TestPgp _pgp = new();
    private readonly FakeHashiCorp _server = new();
    private readonly Dictionary<string, string> _external = [];
    private string? _externalVersion;
    private readonly string _projectId;

    // Autoriza antes de qualquer serviço: o autorizador lê o registro de projetos ao nascer.
    public IacRuntimeTests() => _projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;

    public void Dispose()
    {
        _pgp.Dispose();
        _project.Dispose();
    }

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private string Project() => _projectId;

    private HashiCorpTerraformToolchain Toolchain() =>
        new(_project.DataDir, new MapLocator(_external), _server, Windows, _pgp.Keyring());

    private IacRuntimeService Service(HashiCorpTerraformToolchain? toolchain = null) =>
        new([toolchain ?? Toolchain()], new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), Runner(), Workspace(), _clock, _audit);

    // Responde a `terraform version -json` como o binário externo responderia.
    private InterceptingRunner Runner() => new((spec, _) => Task.FromResult<ProcessResult?>(
        spec.Arguments.SequenceEqual(["version", "-json"]) && _externalVersion is not null
            ? new ProcessResult(0, $$"""{"terraform_version":"{{_externalVersion}}","platform":"windows_amd64"}""", "", false, false)
            : new ProcessResult(1, "", "inesperado", false, false)));

    private void Publish(string version) => _server.Publish(version, Windows.Key, FakeHashiCorp.Zip(("terraform.exe", $"binario {version}")), _pgp);

    private string ExternalBinary(string version)
    {
        var path = Path.Combine(_project.DataDir, "fora", "terraform.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"externo {version}");
        _external["terraform"] = path;
        _externalVersion = version;
        return path;
    }

    [Theory]
    [InlineData(">= 1.11", "1.11.0", true)]
    [InlineData(">= 1.11", "1.10.9", false)]
    [InlineData("~> 1.9", "1.17.2", true)]
    [InlineData("~> 1.9", "2.0.0", false)]
    [InlineData("~> 1.9.2", "1.9.7", true)]
    [InlineData("~> 1.9.2", "1.10.0", false)]
    [InlineData(">= 1.5, < 1.12", "1.11.4", true)]
    [InlineData(">= 1.5, < 1.12", "1.12.0", false)]
    [InlineData("1.16.5", "1.16.5", true)]
    [InlineData("!= 1.16.5", "1.16.5", false)]
    public void Restricao_de_versao_segue_a_sintaxe_do_required_version(string constraint, string version, bool expected) =>
        Assert.Equal(expected, VersionConstraint.TryParse(constraint)!.IsSatisfiedBy(IacVersion.Parse(version)));

    [Fact]
    public void Versao_candidata_vem_antes_da_final()
    {
        Assert.True(IacVersion.Parse("1.17.0-rc2") < IacVersion.Parse("1.17.0"));
        Assert.Null(VersionConstraint.TryParse(">= um"));
    }

    [Fact]
    public void Assinatura_real_da_HashiCorp_e_aceita_e_qualquer_byte_trocado_e_recusado()
    {
        var keyring = OpenPgpKeyring.FromArmored(File.ReadAllText(Path.Combine(ContractFiles.Root,
            "runtime", "Mariliz.Infrastructure", "Iac", "hashicorp-release-key.asc")));
        var fixtures = Path.Combine(ContractFiles.Root, "runtime", "tests", "Mariliz.Application.Tests", "Fixtures", "iac");
        var sums = File.ReadAllBytes(Path.Combine(fixtures, "terraform_1.17.2_SHA256SUMS"));
        var signature = File.ReadAllBytes(Path.Combine(fixtures, "terraform_1.17.2_SHA256SUMS.72D7468F.sig"));

        Assert.Equal("C874011F0AB405110D02105534365D9472D7468F", keyring.PrimaryFingerprint);
        Assert.Equal("374EC75B485913604A831CC7C820C6D5CD27AB87", keyring.Verify(sums, signature));

        var tampered = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(sums).Replace("2244b7a3", "2244b7a4"));
        Assert.Null(keyring.Verify(tampered, signature));
        var brokenSignature = signature.ToArray();
        brokenSignature[^10] ^= 0x01;
        Assert.Null(keyring.Verify(sums, brokenSignature));
        Assert.Null(_pgp.Keyring().Verify(sums, signature));
    }

    [Fact]
    public async Task Sem_Terraform_o_estado_e_ausente_e_a_consulta_nao_fala_com_a_rede()
    {
        var status = await Service().StatusAsync(null, CancellationToken.None);

        Assert.Equal(IacRuntimeState.Missing, status.State);
        Assert.Empty(status.Installations);
        Assert.Null(status.Selected);
        Assert.Empty(_server.Requested);
    }

    [Fact]
    public async Task Recusar_fica_gravado_e_nada_e_instalado_nem_baixado()
    {
        var declined = await Service().DeclineAsync(CancellationToken.None);
        var again = await Service().StatusAsync(null, CancellationToken.None);

        Assert.Equal(IacRuntimeState.Declined, declined.State);
        Assert.Equal(IacRuntimeState.Declined, again.State);
        Assert.Equal(_clock.UtcNow, again.DeclinedAt);
        Assert.Empty(_server.Requested);
        Assert.False(Directory.Exists(Path.Combine(_project.DataDir, "tools", "terraform")));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.declined");
    }

    [Fact]
    public async Task Instalacao_autorizada_baixa_confere_e_fica_fora_do_PATH()
    {
        Publish("1.16.5");
        Publish("1.17.2");
        var service = Service();
        await service.DeclineAsync(CancellationToken.None);

        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);
        var status = await service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None);

        Assert.Equal("1.17.2", offer.Version);
        Assert.Equal(_pgp.Fingerprint, offer.SigningKey);
        Assert.False(offer.IsUpdate);
        Assert.Equal(IacRuntimeState.Ready, status.State);
        Assert.Null(status.DeclinedAt);
        var installed = Assert.Single(status.Installations);
        Assert.Equal(IacRuntimeSource.Managed, installed.Source);
        Assert.True(installed.Verified);
        Assert.Equal(Path.Combine(_project.DataDir, "tools", "terraform", "1.17.2", "terraform.exe"), installed.Path);
        Assert.Equal("binario 1.17.2", File.ReadAllText(installed.Path));
        Assert.DoesNotContain(Path.GetDirectoryName(installed.Path)!, Environment.GetEnvironmentVariable("PATH") ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetDirectories(Path.Combine(_project.DataDir, "tools", "terraform"), ".staging-*"));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.installed" && e.Detail.Contains(_pgp.Fingerprint));
    }

    [Fact]
    public async Task Autorizacao_vale_so_para_a_proposta_mostrada()
    {
        Publish("1.17.2");
        var service = Service();
        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);

        var otherVersion = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync(offer.OfferId, "1.16.5", IacEngine.Terraform, CancellationToken.None));
        var forged = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync("000000000000000000000000", "1.17.2", IacEngine.Terraform, CancellationToken.None));

        Assert.Equal("IAC_VERSION_NOT_PUBLISHED", otherVersion.Code);
        Assert.Equal("IAC_OFFER_STALE", forged.Code);
        Assert.Empty(Toolchain().Managed());
    }

    [Fact]
    public async Task Pacote_que_nao_confere_com_o_checksum_assinado_nao_e_instalado()
    {
        _server.Publish("1.17.2", Windows.Key, FakeHashiCorp.Zip(("terraform.exe", "original")), _pgp);
        var service = Service();
        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);
        _server.Files[FakeHashiCorp.Url("1.17.2", "terraform_1.17.2_windows_amd64.zip")] = FakeHashiCorp.Zip(("terraform.exe", "trocado"));

        var error = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None));

        Assert.Equal("IAC_CHECKSUM_MISMATCH", error.Code);
        Assert.Empty(Toolchain().Managed());
        Assert.Empty(Directory.GetDirectories(Path.Combine(_project.DataDir, "tools", "terraform")));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.install_failed" && e.Detail.Contains("IAC_CHECKSUM_MISMATCH"));
    }

    [Fact]
    public async Task Lista_de_checksums_sem_assinatura_valida_e_recusada()
    {
        _server.Publish("1.17.2", Windows.Key, FakeHashiCorp.Zip(("terraform.exe", "x")), _pgp, tamperSums: sums => sums.Replace("terraform_", "terraform-"));
        var service = Service();
        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);

        var error = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None));

        Assert.Equal("IAC_SIGNATURE_INVALID", error.Code);
        Assert.Empty(Toolchain().Managed());
    }

    [Fact]
    public async Task Pacote_sem_o_binario_na_raiz_nao_extrai_nada()
    {
        _server.Publish("1.17.2", Windows.Key, FakeHashiCorp.Zip(("../terraform.exe", "fuga"), ("LICENSE.txt", "licenca")), _pgp);
        var service = Service();
        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);

        var error = await Assert.ThrowsAsync<EngineException>(() => service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None));

        Assert.Equal("IAC_ARCHIVE_INVALID", error.Code);
        Assert.False(File.Exists(Path.Combine(_project.DataDir, "tools", "terraform.exe")));
    }

    [Fact]
    public async Task Binario_alterado_depois_da_instalacao_deixa_de_ser_usado()
    {
        Publish("1.17.2");
        var service = Service();
        var offer = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);
        var installed = (await service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None)).Installations.Single();
        File.WriteAllText(installed.Path, "adulterado");

        var status = await service.StatusAsync(Project(), CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => service.ResolveForUseAsync(Project(), CancellationToken.None));

        Assert.False(status.Installations.Single().Verified);
        Assert.NotEqual(IacRuntimeState.Ready, status.State);
        Assert.Equal("IAC_TERRAFORM_UNAVAILABLE", error.Code);

        var again = await service.OfferAsync(installed.Version, null, IacEngine.Terraform, CancellationToken.None);
        var repaired = await service.InstallAsync(again.OfferId, again.Version, IacEngine.Terraform, CancellationToken.None);
        Assert.DoesNotContain(again.Impacts, i => i.Contains("já está instalado"));
        Assert.True(repaired.Installations.Single().Verified);
        Assert.Equal("binario 1.17.2", File.ReadAllText(installed.Path));
    }

    [Fact]
    public async Task Terraform_externo_compativel_e_oferecido_e_so_usado_depois_de_adotado()
    {
        var path = ExternalBinary("1.16.5");
        var service = Service();

        var found = await service.StatusAsync(null, CancellationToken.None);
        var wrongVersion = await Assert.ThrowsAsync<EngineException>(() => service.AdoptAsync("1.16.4", IacEngine.Terraform, CancellationToken.None));
        var adopted = await service.AdoptAsync("1.16.5", IacEngine.Terraform, CancellationToken.None);
        File.WriteAllText(path, "outro binario");
        var changed = await service.StatusAsync(null, CancellationToken.None);

        Assert.Equal(IacRuntimeState.ExternalAvailable, found.State);
        Assert.Null(found.Selected);
        Assert.Equal("IAC_EXTERNAL_CHANGED", wrongVersion.Code);
        Assert.Equal(IacRuntimeState.Ready, adopted.State);
        Assert.Equal(IacRuntimeSource.External, adopted.Selected!.Source);
        Assert.Equal(IacRuntimeState.ExternalAvailable, changed.State);
        Assert.Contains("mudou", changed.Installations.Single().Problem);
        Assert.Empty(_server.Requested);
    }

    [Fact]
    public async Task Externo_abaixo_do_minimo_nao_pode_ser_adotado()
    {
        ExternalBinary("1.4.6");
        var service = Service();

        var status = await service.StatusAsync(null, CancellationToken.None);
        var error = await Assert.ThrowsAsync<EngineException>(() => service.AdoptAsync("1.4.6", IacEngine.Terraform, CancellationToken.None));

        Assert.Equal(IacRuntimeState.Incompatible, status.State);
        Assert.Equal("IAC_VERSION_INCOMPATIBLE", error.Code);
    }

    [Fact]
    public async Task Projeto_le_required_version_e_a_oferta_respeita_a_exigencia()
    {
        _project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.11, < 1.17\"\n}\n");
        _project.Write("infra/.terraform/modules/x/versions.tf", "terraform {\n  required_version = \"< 1.0\"\n}\n");
        Publish("1.16.5");
        Publish("1.17.2");
        var service = Service();

        var status = await service.StatusAsync(Project(), CancellationToken.None);
        var offer = await service.OfferAsync(null, Project(), IacEngine.Terraform, CancellationToken.None);

        var requirement = Assert.Single(status.Project!.Requirements);
        Assert.Equal("infra/providers.tf", requirement.File);
        Assert.Equal(">= 1.11, < 1.17", requirement.Constraint);
        Assert.Equal("1.16.5", offer.Version);
    }

    [Fact]
    public async Task Primeiro_uso_fixa_a_versao_e_atualizacao_nao_troca_o_projeto()
    {
        Publish("1.16.5");
        var service = Service();
        var first = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);
        await service.InstallAsync(first.OfferId, first.Version, IacEngine.Terraform, CancellationToken.None);
        var project = Project();

        var used = await service.ResolveForUseAsync(project, CancellationToken.None);
        Publish("1.17.2");
        var update = await service.OfferAsync(null, null, IacEngine.Terraform, CancellationToken.None);
        await service.InstallAsync(update.OfferId, update.Version, IacEngine.Terraform, CancellationToken.None);
        var after = await service.StatusAsync(project, CancellationToken.None);

        Assert.Equal("1.16.5", used.Version);
        Assert.True(update.IsUpdate);
        Assert.Equal("1.16.5", update.CurrentVersion);
        Assert.Contains(update.Impacts, i => i.Contains("Nenhum projeto troca de versão sozinho"));
        Assert.Contains(update.Impacts, i => i.Contains("1.16.5") && i.Contains(Path.GetFileName(_project.Root)));
        Assert.Equal("1.16.5", after.Project!.Selected!.Version);
        Assert.False(after.Project.Automatic);
        Assert.Equal(2, after.Installations.Count);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.pinned" && e.Detail.Contains("\"automatic\":true"));
    }

    [Fact]
    public async Task Fixar_respeita_a_exigencia_do_projeto_e_remover_versao_em_uso_e_recusado()
    {
        _project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.17\"\n}\n");
        Publish("1.16.5");
        Publish("1.17.2");
        var service = Service();
        foreach (var version in new[] { "1.16.5", "1.17.2" })
        {
            var offer = await service.OfferAsync(version, null, IacEngine.Terraform, CancellationToken.None);
            await service.InstallAsync(offer.OfferId, offer.Version, IacEngine.Terraform, CancellationToken.None);
        }
        var project = Project();

        var incompatible = await Assert.ThrowsAsync<EngineException>(() => service.PinAsync(project, "1.16.5", null, IacEngine.Terraform, CancellationToken.None));
        var pinned = await service.PinAsync(project, "1.17.2", IacRuntimeSource.Managed, IacEngine.Terraform, CancellationToken.None);
        var unconfirmed = await Assert.ThrowsAsync<EngineException>(() => service.RemoveAsync("1.16.5", IacEngine.Terraform, false, CancellationToken.None));
        var inUse = await Assert.ThrowsAsync<EngineException>(() => service.RemoveAsync("1.17.2", IacEngine.Terraform, true, CancellationToken.None));
        var removed = await service.RemoveAsync("1.16.5", IacEngine.Terraform, true, CancellationToken.None);

        Assert.Equal("IAC_PROJECT_INCOMPATIBLE", incompatible.Code);
        Assert.Equal("1.17.2", pinned.Project!.Pin!.Version);
        Assert.Equal("IAC_CONFIRMATION_REQUIRED", unconfirmed.Code);
        Assert.Equal("IAC_VERSION_IN_USE", inUse.Code);
        Assert.Equal("1.17.2", Assert.Single(removed.Installations).Version);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.runtime.removed");
    }

    [Fact]
    public async Task Remover_recusa_nome_de_versao_que_seria_caminho()
    {
        var error = await Assert.ThrowsAsync<EngineException>(() => Service().RemoveAsync("../../x", IacEngine.Terraform, true, CancellationToken.None));
        Assert.Equal("IAC_NOT_INSTALLED", error.Code);
        Assert.Throws<EngineException>(() => Toolchain().Remove(".."));
    }

    [Fact]
    public async Task Ferramenta_MCP_so_consulta_e_nao_oferece_instalacao()
    {
        ExternalBinary("1.16.5");
        var project = Project();
        IacRuntimeService Runtimes(WorkspaceAuthorizer authorizer) => new([Toolchain()], new FileIacSettingsStore(_project.DataDir),
            new IacConfigReader(), Runner(), authorizer, _clock, _audit);
        var tools = new IacMcpTools(Workspace, Runtimes, authorizer => new IacOperationService(Runtimes(authorizer),
            new CliIacRuntimeFactory(Runner(), _project.DataDir), new IacConfigReader(), new FileIacWorkArea(_project.DataDir), authorizer, null, _clock, _audit),
            _project.Root);

        var all = tools.Tools();
        Assert.Equal([IacMcpTools.StatusTool, IacMcpTools.ValidateTool, IacMcpTools.PlanSummaryTool], all.Select(t => t.Info.Name));
        Assert.DoesNotContain(all, t => t.Info.Name.Contains("apply") || t.Info.Name.Contains("install"));
        var tool = all[0];
        var text = await tool.Call(new McpCall(new System.Text.Json.Nodes.JsonObject { ["project"] = project }, "teste", CancellationToken.None));

        Assert.Equal(IacMcpTools.StatusTool, tool.Info.Name);
        Assert.Contains("O Spock pode usá-lo sem instalar outro", text);
        Assert.Contains("só pela tela Infraestrutura", text);
        Assert.Empty(_server.Requested);
        Assert.Empty(new FileIacSettingsStore(_project.DataDir).Read().Adopted);
    }

    private sealed class MapLocator(Dictionary<string, string> map) : IExecutableLocator
    {
        public string? Find(string name) => map.GetValueOrDefault(name);
    }
}
