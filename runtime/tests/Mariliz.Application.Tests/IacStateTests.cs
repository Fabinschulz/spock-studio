using Mariliz.Application.Cloud;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Cloud;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// Entrega 4: onde vive o state, os riscos, o backend recomendado, o isolamento entre projetos, a devolução do state
// local ao projeto (com conflito), a consulta, o force-unlock e o bootstrap do bucket no formato do modelo.
public sealed class IacStateTests : IDisposable
{
    private const string Secret = "AKIASEGREDONUNCALIDO";

    private readonly TempProject _project = TempProject.Dotnet();
    private readonly TempProject _other = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly IacOperationTests.FakeRuntime _runtime = new();
    private readonly string _projectId;
    private readonly string _otherId;

    public IacStateTests()
    {
        _projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        _otherId = Workspace().Authorize(_other.Root).Project!.ProjectId;
        _project.Write("infra/providers.tf", $$"""
            terraform {
              required_version = ">= 1.11"

              backend "s3" {
                bucket               = "org-terraform-state-123456789012-sa-east-1"
                key                  = "terraform.tfstate"
                workspace_key_prefix = "loja"
                region               = "sa-east-1"
                access_key           = "{{Secret}}"
                use_lockfile         = true
              }
            }

            provider "aws" {
              region = "sa-east-1"
            }
            """);
        _project.Write("infra/modules/sqs/resource.tf", "resource \"terraform_data\" \"q\" {}\n");
        _project.Write("infra/bootstrap/providers.tf", "provider \"aws\" {\n  region = \"sa-east-1\"\n}\n");
        _project.Write("infra/bootstrap/terraform.tfstate", "{\"version\":4}");
    }

    public void Dispose()
    {
        _project.Dispose();
        _other.Dispose();
    }

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private CloudHub Cloud(params CloudProjectLink[] links)
    {
        var store = new FileCloudStore(_project.DataDir);
        store.SaveConnection(new CloudConnection("c-aws", "aws", "Dev", new CloudSettings("profile",
                new Dictionary<string, string> { ["profile"] = "dev-sso", ["region"] = "sa-east-1" }), CloudConnectionStatus.Connected,
            new CloudIdentity("123456789012", null, "arn:aws:sts::123456789012:assumed-role/Dev/pessoa", null), [], _clock.UtcNow, _clock.UtcNow, null, "1"));
        foreach (var link in links) store.SaveLink(link);
        return new CloudHub(store, new CloudProviderRegistry([]), Workspace(), _clock, _audit);
    }

    private IacStateService State(CloudHub? cloud = null, InterceptingRunner? runner = null) =>
        new(Workspace(), new IacConfigReader(), new IacStateReader(), new FileIacStateRegistry(_project.DataDir), cloud, new LocalProjectFilesFactory(),
            new FileRunStore(_project.DataDir), runner ?? new InterceptingRunner(), _clock, new RunTracker(), _audit);

    private async Task<IacOperationService> Operations(CloudHub? cloud = null)
    {
        var tofu = Path.Combine(_project.DataDir, "fora", "tofu.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(tofu)!);
        File.WriteAllText(tofu, "tofu de mentira");
        var workspace = Workspace();
        var runner = new InterceptingRunner((_, _) => Task.FromResult<ProcessResult?>(new ProcessResult(0, """{"terraform_version":"1.13.9"}""", "", false, false)));
        var runtimes = new IacRuntimeService([new OpenTofuToolchain(_project.DataDir, new MapLocator(tofu), new FakeOpenTofu(), new IacPlatform("windows", "amd64"))],
            new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), runner, workspace, _clock, _audit);
        await runtimes.AdoptAsync("1.13.9", IacEngine.OpenTofu, CancellationToken.None);
        return new IacOperationService(runtimes, new IacOperationTests.FakeFactory(_runtime), new IacConfigReader(), new FileIacWorkArea(_project.DataDir),
            workspace, cloud, _clock, _audit, new IacStateReader(), new FileIacStateRegistry(_project.DataDir));
    }

    [Fact]
    public void Backend_e_lido_como_texto_sem_credencial()
    {
        var reader = new IacStateReader();

        var backend = reader.Backend(_project.Root, "infra")!;

        Assert.Equal("s3", backend.Type);
        Assert.Equal("loja", backend.Settings["workspace_key_prefix"]);
        Assert.Equal("true", backend.Settings["use_lockfile"]);
        Assert.False(backend.Settings.ContainsKey("access_key"));
        Assert.DoesNotContain(Secret, string.Join(",", backend.Settings.Values));
        Assert.True(reader.IsRoot(_project.Root, "infra"));
        Assert.True(reader.IsRoot(_project.Root, "infra/bootstrap"));
        Assert.False(reader.IsRoot(_project.Root, "infra/modules/sqs"));
        Assert.Equal(["terraform.tfstate"], reader.LocalStateFiles(_project.Root, "infra/bootstrap"));
        Assert.Equal("s3://org-terraform-state-123456789012-sa-east-1/loja/terraform.tfstate", IacStateIdentity.Of(backend));
    }

    [Fact]
    public void Backend_salvo_com_CRLF_tambem_e_lido()
    {
        var text = File.ReadAllText(Path.Combine(_project.Root, "infra", "providers.tf")).Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(Path.Combine(_project.Root, "infra", "providers.tf"), text);

        var backend = new IacStateReader().Backend(_project.Root, "infra")!;

        Assert.Equal("loja", backend.Settings["workspace_key_prefix"]);
        Assert.Equal("true", backend.Settings["use_lockfile"]);
    }

    [Fact]
    public async Task Relatorio_aponta_raizes_riscos_e_state_que_iria_para_o_git()
    {
        _project.Git("init", "-q");
        var report = await State(runner: new InterceptingRunner()).ReportAsync(_projectId, CancellationToken.None);

        Assert.Equal(["infra", "infra/bootstrap"], report.Roots.Select(r => r.Folder));
        var main = report.Roots[0];
        Assert.Equal((IacStateMode.Remote, true, false), (main.Mode, main.Locking, main.Encrypted));
        Assert.Contains(main.Risks, r => r is { Rule: "not-encrypted", Level: IacStateRiskLevel.Warning });
        var bootstrap = report.Roots[1];
        Assert.Equal(IacStateMode.LocalInProject, bootstrap.Mode);
        Assert.Contains(bootstrap.Risks, r => r is { Rule: "state-in-git", Level: IacStateRiskLevel.Danger });
        Assert.Contains(bootstrap.Risks, r => r.Rule == "bootstrap-local");
        Assert.Null(report.Recommendation);
        Assert.DoesNotContain(Secret, System.Text.Json.JsonSerializer.Serialize(report));
    }

    [Fact]
    public async Task State_local_na_raiz_principal_recebe_recomendacao_do_backend_da_nuvem_vinculada()
    {
        _project.Write("infra/providers.tf", "provider \"aws\" {\n  region = \"sa-east-1\"\n}\n");
        var cloud = Cloud(new CloudProjectLink("l-1", _projectId, "c-aws", "aws", [], false, _clock.UtcNow));

        var report = await State(cloud).ReportAsync(_projectId, CancellationToken.None);

        Assert.Equal(("aws", "s3"), (report.Recommendation!.Provider, report.Recommendation.Backend));
        Assert.Contains(report.Roots[0].Risks, r => r.Rule == "local-state");
    }

    [Fact]
    public async Task Dois_projetos_nao_planejam_sobre_o_mesmo_state()
    {
        _other.Write("infra/providers.tf", File.ReadAllText(Path.Combine(_project.Root, "infra", "providers.tf")));
        var operations = await Operations();
        await operations.PlanAsync(new IacPlanRequest(_projectId, "infra", null, null), IacOperationService.Person, CancellationToken.None);

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            operations.PlanAsync(new IacPlanRequest(_otherId, "infra", null, null), IacOperationService.Person, CancellationToken.None));
        var report = await State().ReportAsync(_otherId, CancellationToken.None);

        Assert.Equal("IAC_STATE_SHARED", error.Code);
        Assert.Contains(Path.GetFileName(_project.Root), error.Message);
        Assert.Contains(report.Roots[0].Risks, r => r is { Rule: "shared", Level: IacStateRiskLevel.Danger });
    }

    [Fact]
    public async Task State_local_volta_ao_projeto_depois_do_apply_com_backup()
    {
        _runtime.PlanStateMode = IacStateMode.LocalInProject;
        _runtime.PlanLocalState = "terraform.tfstate";
        _runtime.ApplyWritesState = "{\"version\":4,\"serial\":2}";
        var operations = await Operations();
        var plan = await operations.PlanAsync(new IacPlanRequest(_projectId, "infra/bootstrap", null, null), IacOperationService.Person, CancellationToken.None);

        var applied = await operations.ApplyAsync(new IacApplyRequest(_projectId, plan.Id, plan.PlanId!, false, false, null), CancellationToken.None);

        var state = Path.Combine(_project.Root, "infra", "bootstrap", "terraform.tfstate");
        Assert.Equal(IacStateMode.LocalInProject, plan.StateMode);
        Assert.NotNull(plan.LocalStateHash);
        Assert.Equal(IacOperationStatus.Succeeded, applied.Status);
        Assert.Equal("{\"version\":4,\"serial\":2}", File.ReadAllText(state));
        Assert.Equal("{\"version\":4}", File.ReadAllText(state + ".backup"));
    }

    [Fact]
    public async Task State_mudado_antes_ou_durante_o_apply_nao_e_sobrescrito()
    {
        _runtime.PlanStateMode = IacStateMode.LocalInProject;
        _runtime.PlanLocalState = "terraform.tfstate";
        _runtime.ApplyWritesState = "{\"version\":4,\"serial\":2}";
        var operations = await Operations();
        var state = Path.Combine(_project.Root, "infra", "bootstrap", "terraform.tfstate");

        var before = await operations.PlanAsync(new IacPlanRequest(_projectId, "infra/bootstrap", null, null), IacOperationService.Person, CancellationToken.None);
        File.WriteAllText(state, "{\"version\":4,\"serial\":9}");
        var changed = await Assert.ThrowsAsync<EngineException>(() =>
            operations.ApplyAsync(new IacApplyRequest(_projectId, before.Id, before.PlanId!, false, false, null), CancellationToken.None));

        var during = await operations.PlanAsync(new IacPlanRequest(_projectId, "infra/bootstrap", null, null), IacOperationService.Person, CancellationToken.None);
        _runtime.DuringApply = () => File.WriteAllText(state, "{\"outra\":\"pessoa\"}");
        var conflict = await operations.ApplyAsync(new IacApplyRequest(_projectId, during.Id, during.PlanId!, false, false, null), CancellationToken.None);

        Assert.Equal("IAC_STATE_CHANGED", changed.Code);
        Assert.Equal("{\"outra\":\"pessoa\"}", File.ReadAllText(state));
        Assert.Contains("não foi sobrescrito", conflict.Error);
        var recovered = Directory.GetFiles(Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations"), "terraform.tfstate", SearchOption.AllDirectories);
        Assert.Equal("{\"version\":4,\"serial\":2}", File.ReadAllText(Assert.Single(recovered)));
    }

    [Fact]
    public async Task Consulta_do_state_devolve_so_enderecos_e_registra()
    {
        var operations = await Operations();

        var read = await operations.ReadStateAsync(_projectId, "infra", "dev", null, IacOperationService.Person, CancellationToken.None);

        Assert.Equal(["aws_s3_bucket.tf_state"], read.Addresses);
        Assert.Equal(["default", "dev"], read.Workspaces);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.state_read" && e.Detail.Contains("\"resources\":1"));
    }

    [Fact]
    public async Task Plano_travado_mostra_o_id_e_liberar_pede_confirmacao()
    {
        _runtime.PlanError = "Error: Error acquiring the state lock\n\nLock Info:\n  ID:        3f1c2a9e-1b2c-4d5e-8f90-123456789abc\n  Who:       pessoa@maquina";
        var operations = await Operations();
        var plan = await operations.PlanAsync(new IacPlanRequest(_projectId, "infra", null, null), IacOperationService.Person, CancellationToken.None);

        var unconfirmed = await Assert.ThrowsAsync<EngineException>(() =>
            operations.UnlockAsync(_projectId, "infra", null, null, plan.LockId!, false, CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<EngineException>(() =>
            operations.UnlockAsync(_projectId, "infra", null, null, "x; rm -rf /", true, CancellationToken.None));
        await operations.UnlockAsync(_projectId, "infra", null, null, plan.LockId!, true, CancellationToken.None);

        Assert.Equal("3f1c2a9e-1b2c-4d5e-8f90-123456789abc", plan.LockId);
        Assert.Equal("IAC_UNLOCK_CONFIRMATION_REQUIRED", unconfirmed.Code);
        Assert.Equal("IAC_LOCK_ID_INVALID", invalid.Code);
        Assert.Equal(plan.LockId, _runtime.LastUnlock!.Value.LockId);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.state_unlocked" && e.Detail.Contains(plan.LockId!));
    }

    [Fact]
    public async Task Bootstrap_AWS_segue_o_modelo_e_usa_a_conexao_vinculada()
    {
        File.Delete(Path.Combine(_project.Root, "infra", "bootstrap", "providers.tf"));
        var cloud = Cloud(new CloudProjectLink("l-1", _projectId, "c-aws", "aws", [], false, _clock.UtcNow));
        var request = new IacBootstrapRequest(_projectId, "aws", "c-aws",
            new Dictionary<string, string> { ["bucket"] = "loja-terraform-state-123456789012-sa-east-1", ["project"] = "loja" });

        var plan = State(cloud).BootstrapPlan(request);

        Assert.Equal("infra/bootstrap", plan.Folder);
        Assert.Equal(["infra/bootstrap/providers.tf", "infra/bootstrap/variables.tf", "infra/bootstrap/resource.tf", "infra/bootstrap/output.tf", ".gitignore"],
            plan.Changes.Select(c => c.Path));
        var diff = string.Join("\n", plan.Changes.Select(c => c.Diff));
        Assert.Contains("allowed_account_ids = [\"123456789012\"]", diff);
        Assert.Contains("default     = \"dev-sso\"", diff);
        Assert.Contains($"version = \"{IacBootstrapTemplates.AwsVersion}\"", diff);
        Assert.Contains("prevent_destroy = true", diff);
        Assert.Contains("block_public_policy     = true", diff);
        Assert.Contains("*.tfstate", diff);
        Assert.Contains("workspace_key_prefix = \"loja\"", plan.BackendBlock);
        Assert.Contains("use_lockfile         = true", plan.BackendBlock);
    }

    [Fact]
    public async Task Bootstrap_grava_so_com_o_plano_revisado_e_vira_execucao_reversivel()
    {
        var state = State();
        var request = new IacBootstrapRequest(_projectId, "gcp", null,
            new Dictionary<string, string> { ["bucket"] = "loja-tf-state", ["gcpProject"] = "loja-dev-123", ["location"] = "southamerica-east1" });
        var plan = state.BootstrapPlan(request);

        var stale = Assert.Throws<EngineException>(() => state.StartBootstrap(request with
        {
            Values = new Dictionary<string, string>(request.Values) { ["bucket"] = "outro-bucket" }
        }, plan.PlanId));
        var run = state.StartBootstrap(request, plan.PlanId);
        await run.Completion!;

        Assert.Equal("STALE_PLAN", stale.Code);
        var resource = File.ReadAllText(Path.Combine(_project.Root, "infra", "bootstrap", "resource.tf"));
        Assert.Contains("public_access_prevention    = \"enforced\"", resource);
        Assert.Contains("prevent_destroy = true", resource);
        Assert.True(RunKinds.IsRevertible(RunKinds.Infrastructure));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.bootstrap_written" && e.Detail.Contains("\"provider\":\"gcp\""));
    }

    [Theory]
    [InlineData("aws", "bucket", "Bucket_Invalido")]
    [InlineData("azure", "storageAccount", "Conta-Com-Hifen")]
    [InlineData("gcp", "gcpProject", "x")]
    public void Valor_fora_do_formato_nao_gera_arquivo(string provider, string field, string value)
    {
        var values = new Dictionary<string, string>
        {
            ["bucket"] = "loja-tf-state", ["region"] = "sa-east-1", ["resourceGroup"] = "rg-state", ["storageAccount"] = "lojatfstate",
            ["location"] = "brazilsouth", ["gcpProject"] = "loja-dev-123", [field] = value
        };

        var error = Assert.Throws<EngineException>(() => State().BootstrapPlan(new IacBootstrapRequest(_projectId, provider, null, values)));

        Assert.Equal("IAC_BOOTSTRAP_INVALID", error.Code);
    }

    [Theory]
    [InlineData("infra/bootstrap/resource.tf", true)]
    [InlineData("infra/main.tofu", true)]
    [InlineData(".gitignore", true)]
    [InlineData("src/Program.cs", false)]
    [InlineData("infra/terraform.tfstate", false)]
    [InlineData("infra/.terraform/modules/x.tf", false)]
    [InlineData(".git/hooks/pre-commit.tf", false)]
    [InlineData("infra/terraform.tfvars", false)]
    [InlineData("infra/terraform.tfvars.example", true)]
    [InlineData("infra/README.md", true)]
    [InlineData("README.md", false)]
    [InlineData("infra/notas.md", false)]
    public void Escopo_de_infraestrutura_so_grava_tf_tofu_exemplo_readme_e_gitignore(string path, bool writable) =>
        Assert.Equal(writable, Mariliz.Core.Policy.ManagedScope.IsInfrastructureWritable(path));

    private sealed class MapLocator(string tofu) : IExecutableLocator
    {
        public string? Find(string name) => name == "tofu" ? tofu : null;
    }
}
