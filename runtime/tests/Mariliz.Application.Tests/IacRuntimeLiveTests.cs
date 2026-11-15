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

    // Entrega 2 de ponta a ponta com o OpenTofu de verdade, sem nuvem: terraform_data é do próprio OpenTofu (nenhum provider
    // a baixar) e o state é local. Valida (com um arquivo fora do formato e um erro de verdade), planeja e confere que o
    // projeto ficou como estava.
    [LiveFact(Variable)]
    public async Task OpenTofu_valida_e_planeja_sobre_uma_copia_sem_tocar_o_projeto()
    {
        using var project = TempProject.Dotnet();
        project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.6\"\n}\n");
        project.Write("infra/resource.tf",
            "variable \"ambiente\" {\n  type    = string\n  default = \"dev\"\n}\n\nresource \"terraform_data\" \"fila\" {\n  input = \"fila-${var.ambiente}\"\n}\n\nmodule \"rede\" {\n  source = \"./modules/rede\"\n}\n\noutput \"fila\" {\n  value = terraform_data.fila.output\n}\n");
        project.Write("infra/modules/rede/resource.tf", "resource \"terraform_data\" \"rede\" {\n  input = \"10.0.0.0/16\"\n}\n");
        var before = project.Fingerprint();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var audit = new MemoryAudit();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, null, new FakeClock(), audit);

        var valid = await operations.ValidateAsync(projectId, IacOperationService.Person, CancellationToken.None);
        var plan = await operations.PlanAsync(new IacPlanRequest(projectId, "infra", null, null), IacOperationService.Person, CancellationToken.None);
        project.Write("infra/modules/rede/resource.tf", "resource \"terraform_data\" \"rede\" {\ninput = \"10.0.0.0/16\"\n  nome = \"x\"\n}\n");
        var broken = await operations.ValidateAsync(projectId, IacOperationService.Person, CancellationToken.None);

        output.WriteLine($"validate: {valid.Status}; plan: {plan.Status} {plan.Error} criar={plan.Summary?.Create}; quebrado: {broken.Error}");
        foreach (var d in broken.Validation!.SelectMany(v => v.Diagnostics)) output.WriteLine($"  {d.Severity} {d.Summary} {d.File}:{d.Line}");
        Assert.Equal(IacOperationStatus.Succeeded, valid.Status);
        Assert.Equal(IacOperationStatus.Succeeded, plan.Status);
        Assert.True(plan.HasChanges);
        Assert.Equal(2, plan.Summary!.Create);
        Assert.Contains(plan.Summary.Changes, c => c.Address == "module.rede.terraform_data.rede");
        Assert.Equal(["fila"], plan.Summary.OutputsChanged);
        Assert.Equal(IacOperationStatus.Failed, broken.Status);
        var folder = broken.Validation.Single(v => v.Folder == "infra/modules/rede");
        Assert.False(folder.Formatted);
        Assert.False(folder.Valid);
        Assert.Contains(folder.Diagnostics, d => d.Severity == "error" && d.Line == 3);
        project.Write("infra/modules/rede/resource.tf", "resource \"terraform_data\" \"rede\" {\n  input = \"10.0.0.0/16\"\n}\n");
        Assert.Equal(before, project.Fingerprint());
        Assert.False(Directory.Exists(Path.Combine(project.Root, "infra", ".terraform")));
    }

    private sealed class NoExternal : Application.Ports.IExecutableLocator
    {
        public string? Find(string name) => null;
    }
}
