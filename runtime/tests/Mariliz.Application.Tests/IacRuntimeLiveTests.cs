using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
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

    // Entrega 3 de ponta a ponta com o OpenTofu de verdade: aprovar e aplicar um plano, provar com um plano seguinte sem
    // mudanças, recusar plano sem mudanças, exigir confirmação para destruir e recusar aplicar quando o state ficaria na
    // cópia. O backend local aponta para fora do projeto, como um backend remoto faria.
    [LiveFact(Variable)]
    public async Task OpenTofu_aplica_so_o_plano_aprovado_e_prova_o_resultado()
    {
        using var project = TempProject.Dotnet();
        var stateDirectory = Directory.CreateTempSubdirectory("spock-state-").FullName;
        var statePath = Path.Combine(stateDirectory, "terraform.tfstate").Replace('\\', '/');
        project.Write("infra/providers.tf", $"terraform {{\n  required_version = \">= 1.6\"\n\n  backend \"local\" {{\n    path = \"{statePath}\"\n  }}\n}}\n");
        project.Write("infra/resource.tf", "resource \"terraform_data\" \"fila\" {\n  input = \"fila-dev\"\n}\n\nresource \"terraform_data\" \"rede\" {\n  input = \"10.0.0.0/16\"\n}\n\noutput \"fila\" {\n  value = terraform_data.fila.output\n}\n");
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var audit = new MemoryAudit();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, null, new SystemClock(), audit);
        IacApplyRequest Approve(IacOperation plan, bool destroy = false) => new(projectId, plan.Id, plan.PlanId!, destroy, false, null);
        Task<IacOperation> Plan() => operations.PlanAsync(new IacPlanRequest(projectId, "infra", null, null), IacOperationService.Person, CancellationToken.None);

        var first = await Plan();
        var applied = await operations.ApplyAsync(Approve(first), CancellationToken.None);
        var nothing = await Plan();
        var noChanges = await Assert.ThrowsAsync<EngineException>(() => operations.ApplyAsync(Approve(nothing), CancellationToken.None));

        project.Write("infra/resource.tf", "resource \"terraform_data\" \"fila\" {\n  input = \"fila-dev\"\n}\n\noutput \"fila\" {\n  value = terraform_data.fila.output\n}\n");
        var removal = await Plan();
        var unconfirmed = await Assert.ThrowsAsync<EngineException>(() => operations.ApplyAsync(Approve(removal), CancellationToken.None));
        var destroyed = await operations.ApplyAsync(Approve(removal, destroy: true), CancellationToken.None);

        // State local padrão (entrega 4): o apply roda na cópia e o state volta ao projeto, com o anterior em .backup.
        project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.6\"\n}\n");
        var localState = await Plan();
        var local = await operations.ApplyAsync(Approve(localState), CancellationToken.None);
        var afterLocal = await Plan();

        output.WriteLine($"apply: {applied.Status} verificado={applied.Apply!.Verified} recursos={applied.Apply.ManagedResources} outputs={string.Join(",", applied.Apply.Outputs)}");
        output.WriteLine($"destruição: {destroyed.Status} verificado={destroyed.Apply!.Verified} recursos={destroyed.Apply.ManagedResources}; state local: {local.Status} {local.Error}");
        Assert.Equal(IacOperationStatus.Succeeded, applied.Status);
        Assert.True(applied.Apply.Verified);
        Assert.Equal(2, applied.Apply.ManagedResources);
        Assert.Equal("fila-dev", applied.Apply.Outputs["fila"]);
        Assert.True(File.Exists(statePath));
        Assert.False(nothing.HasChanges);
        Assert.Equal("IAC_PLAN_NO_CHANGES", noChanges.Code);
        Assert.Equal(1, removal.Summary!.Delete);
        Assert.Equal("IAC_DESTROY_CONFIRMATION_REQUIRED", unconfirmed.Code);
        Assert.True(destroyed.Apply.Verified);
        Assert.Equal(1, destroyed.Apply.ManagedResources);
        Assert.Equal(IacStateMode.LocalInProject, localState.StateMode);
        Assert.Null(localState.ApplyBlocker);
        Assert.True(local.Apply!.Verified);
        Assert.True(File.Exists(Path.Combine(project.Root, "infra", "terraform.tfstate")));
        Assert.False(afterLocal.HasChanges);
        Assert.False(Directory.Exists(Path.Combine(project.Root, "infra", ".terraform")));
        Directory.Delete(stateDirectory, recursive: true);
    }

    // Os bootstraps gerados para AWS, Azure e Google passam no validate do OpenTofu de verdade. Baixa os três providers
    // oficiais (algumas centenas de MB): só com MARILIZ_LIVE_IAC_PROVIDERS=1 também definido.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS")]
    public async Task Bootstraps_gerados_passam_no_validate_do_OpenTofu()
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var audit = new MemoryAudit();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var state = new IacStateService(workspace, new IacConfigReader(), new IacStateReader(), new FileIacStateRegistry(project.DataDir), null,
            new LocalProjectFilesFactory(), new FileRunStore(project.DataDir), runner, new SystemClock(), new RunTracker(), audit);
        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, null, new SystemClock(), audit);

        var requests = new Dictionary<string, Dictionary<string, string>>
        {
            ["aws"] = new() { ["bucket"] = "loja-terraform-state-123456789012-sa-east-1", ["region"] = "sa-east-1", ["account"] = "123456789012" },
            ["azure"] = new() { ["resourceGroup"] = "rg-loja-state", ["storageAccount"] = "lojatfstate", ["location"] = "brazilsouth",
                ["subscription"] = "00000000-0000-0000-0000-000000000000" },
            ["gcp"] = new() { ["bucket"] = "loja-tf-state", ["gcpProject"] = "loja-dev-123", ["location"] = "southamerica-east1" }
        };
        foreach (var (provider, values) in requests)
        {
            foreach (var file in Directory.Exists(Path.Combine(project.Root, "infra")) ? Directory.GetFiles(Path.Combine(project.Root, "infra"), "*.tf", SearchOption.AllDirectories) : [])
                File.Delete(file);
            var request = new IacBootstrapRequest(projectId, provider, null, values);
            var plan = state.BootstrapPlan(request);
            await state.StartBootstrap(request, plan.PlanId).Completion!;
            var validation = await operations.ValidateAsync(projectId, IacOperationService.Person, CancellationToken.None);
            var folder = validation.Validation!.Single();
            output.WriteLine($"{provider}: {validation.Status} {validation.Error} {folder.Error} " + string.Join(" | ", folder.Diagnostics.Select(d => $"{d.Severity} {d.Summary} {d.Detail} {d.File}:{d.Line}")));
            Assert.Equal(IacOperationStatus.Succeeded, validation.Status);
        }
    }

    // O Terraform gerado a partir do desenho completo (serviço, banco e filas) e do mínimo (só filas) passa no fmt e no
    // validate do OpenTofu, raiz e cada módulo, com o provider AWS de verdade.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS")]
    public async Task Terraform_gerado_passa_no_fmt_e_no_validate_do_OpenTofu()
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var audit = new MemoryAudit();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, null, new SystemClock(), audit);

        foreach (var blueprint in new[] { IacBlueprintSamples.Full(), IacBlueprintSamples.QueuesOnly() })
        {
            var infra = Path.Combine(project.Root, "infra");
            if (Directory.Exists(infra)) Directory.Delete(infra, recursive: true);
            foreach (var file in IacAwsGenerator.Generate(blueprint, "infra"))
            {
                var path = Path.Combine(project.Root, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, file.Content);
            }
            var validation = await operations.ValidateAsync(projectId, IacOperationService.Person, CancellationToken.None);
            output.WriteLine($"{blueprint.Project}: {validation.Status} {validation.Error}");
            foreach (var folder in validation.Validation ?? [])
                output.WriteLine($"  {folder.Folder}: valid={folder.Valid} fmt={folder.Formatted} {folder.Error} " + string.Join(" | ", folder.Diagnostics.Select(d => $"{d.Severity} {d.Summary} {d.Detail} {d.File}:{d.Line}")));
            Assert.Equal(IacOperationStatus.Succeeded, validation.Status);
        }
    }

    private sealed class NoExternal : Application.Ports.IExecutableLocator
    {
        public string? Find(string name) => null;
    }
}
