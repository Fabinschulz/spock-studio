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

// As políticas da camada Spock Infrastructure Management com uma ferramenta de mentira atrás da IIacRuntime: cópia da
// configuração, projeto intocado, pastas e ambientes válidos, conexão vinculada, uma operação por vez, histórico e trilha.
public sealed class IacOperationTests : IDisposable
{
    private const string StateSecret = "segredo-do-state";

    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly FakeRuntime _runtime = new();
    private readonly Dictionary<string, string> _external = [];
    private readonly string _projectId;

    public IacOperationTests()
    {
        _projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        _project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.11\"\n}\n");
        _project.Write("infra/resource.tf", "module \"sqs\" {\n  source = \"./modules/sqs\"\n}\n");
        _project.Write("infra/modules/sqs/resource.tf", "resource \"terraform_data\" \"q\" {}\n");
        _project.Write("infra/modules/sqs/policy.json", "{}");
        _project.Write("infra/terraform.tfstate", $"{{\"secret\":\"{StateSecret}\"}}");
        _project.Write("infra/.terraform/providers/lixo.txt", "não copiar");
    }

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private InterceptingRunner Runner() => new((spec, _) => Task.FromResult<ProcessResult?>(
        new ProcessResult(0, """{"terraform_version":"1.13.9","platform":"windows_amd64"}""", "", false, false)));

    private async Task<IacOperationService> Service(CloudHub? cloud = null)
    {
        var tofu = Path.Combine(_project.DataDir, "fora", "tofu.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(tofu)!);
        File.WriteAllText(tofu, "tofu de mentira");
        _external["tofu"] = tofu;
        var workspace = Workspace();
        var runtimes = new IacRuntimeService([new OpenTofuToolchain(_project.DataDir, new MapLocator(_external), new FakeOpenTofu(), new IacPlatform("windows", "amd64"))],
            new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), Runner(), workspace, _clock, _audit);
        await runtimes.AdoptAsync("1.13.9", IacEngine.OpenTofu, CancellationToken.None);
        return new IacOperationService(runtimes, new FakeFactory(_runtime), new IacConfigReader(), new FileIacWorkArea(_project.DataDir), workspace, cloud, _clock, _audit);
    }

    [Fact]
    public async Task Layout_encontra_a_raiz_e_as_pastas_do_modelo()
    {
        var layout = (await Service()).Layout(_projectId);

        Assert.Equal("infra", layout.Root);
        Assert.Equal(["infra", "infra/modules/sqs"], layout.Folders);
    }

    [Fact]
    public async Task Validar_roda_numa_copia_sem_state_e_nao_toca_o_projeto()
    {
        var before = _project.Fingerprint();
        var service = await Service();

        var operation = await service.ValidateAsync(_projectId, IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationStatus.Succeeded, operation.Status);
        Assert.Equal(IacEngine.OpenTofu, operation.Engine);
        Assert.Equal(["infra", "infra/modules/sqs"], operation.Validation!.Select(v => v.Folder));
        Assert.True(_runtime.SawConfig);
        Assert.False(_runtime.SawState);
        Assert.False(_runtime.SawDotTerraform);
        Assert.Equal(before, _project.Fingerprint());
        Assert.False(Directory.Exists(Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", operation.Id)));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.validate" && e.Detail.Contains("\"folders\":2"));
        Assert.Equal(operation.Id, Assert.Single(service.History(_projectId)).Id);
    }

    [Fact]
    public async Task Arquivo_fora_do_formato_reprova_a_validacao_da_pasta()
    {
        _runtime.Unformatted = ["modules/sqs/resource.tf"];
        var operation = await (await Service()).ValidateAsync(_projectId, IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationStatus.Failed, operation.Status);
        Assert.True(operation.Validation!.Single(v => v.Folder == "infra").Formatted);
        Assert.False(operation.Validation!.Single(v => v.Folder == "infra/modules/sqs").Formatted);
        Assert.Contains("infra/modules/sqs/resource.tf", operation.Error);
    }

    [Fact]
    public async Task Planejar_le_a_copia_do_state_local_e_apaga_a_copia_depois()
    {
        var before = _project.Fingerprint();
        var service = await Service();

        var operation = await service.PlanAsync(new IacPlanRequest(_projectId, null, "dev", null), IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationStatus.Succeeded, operation.Status);
        Assert.Equal("infra", operation.Folder);
        Assert.Equal("dev", _runtime.LastPlan!.Workspace);
        Assert.True(_runtime.SawState);
        var directory = Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", operation.Id);
        Assert.True(File.Exists(Path.Combine(directory, "plan.tfplan")));
        Assert.Empty(Directory.GetFiles(directory, "terraform.tfstate*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(directory, "data")));
        Assert.Equal(16, operation.PlanId!.Length);
        Assert.Equal(1, operation.Summary!.Create);
        Assert.Equal(before, _project.Fingerprint());
        Assert.DoesNotContain(_audit.Entries, e => e.Detail.Contains(StateSecret));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.plan" && e.Detail.Contains("\"create\":1") && e.Detail.Contains("\"localState\":true"));
    }

    [Theory]
    [InlineData("../fora", null)]
    [InlineData("infra/.terraform", null)]
    [InlineData(null, "Prod")]
    [InlineData(null, "dev; rm -rf /")]
    public async Task Pasta_fora_da_configuracao_e_ambiente_invalido_sao_recusados(string? folder, string? workspace)
    {
        var service = await Service();

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            service.PlanAsync(new IacPlanRequest(_projectId, folder, workspace, null), IacOperationService.Person, CancellationToken.None));

        Assert.Contains(error.Code, new[] { "IAC_FOLDER_UNKNOWN", "IAC_WORKSPACE_INVALID" });
        Assert.Null(_runtime.LastPlan);
    }

    [Fact]
    public async Task Conexao_vinculada_passa_so_nomes_e_nao_vinculada_e_recusada()
    {
        var store = new FileCloudStore(_project.DataDir);
        store.SaveConnection(new CloudConnection("c-aws", "aws", "Dev", new CloudSettings("profile",
            new Dictionary<string, string> { ["profile"] = "dev-sso", ["region"] = "sa-east-1" }), CloudConnectionStatus.Connected, null, [],
            _clock.UtcNow, _clock.UtcNow, null, "1"));
        store.SaveConnection(new CloudConnection("c-outra", "aws", "Outra", new CloudSettings("default", new Dictionary<string, string>()),
            CloudConnectionStatus.Connected, null, [], _clock.UtcNow, _clock.UtcNow, null, "1"));
        store.SaveLink(new CloudProjectLink("l-1", _projectId, "c-aws", "aws", [], false, _clock.UtcNow));
        var cloud = new CloudHub(store, new CloudProviderRegistry([]), Workspace(), _clock, _audit);
        var service = await Service(cloud);

        var refused = await Assert.ThrowsAsync<EngineException>(() =>
            service.PlanAsync(new IacPlanRequest(_projectId, null, null, "c-outra"), IacOperationService.Person, CancellationToken.None));
        await service.PlanAsync(new IacPlanRequest(_projectId, null, null, "c-aws"), IacOperationService.Person, CancellationToken.None);

        Assert.Equal("IAC_CONNECTION_NOT_LINKED", refused.Code);
        Assert.Equal("dev-sso", _runtime.LastPlan!.Environment["AWS_PROFILE"]);
        Assert.Equal("sa-east-1", _runtime.LastPlan.Environment["AWS_REGION"]);
        Assert.DoesNotContain(_runtime.LastPlan.Environment.Keys, k => k.Contains("SECRET") || k.Contains("ACCESS_KEY"));
    }

    [Fact]
    public async Task Uma_operacao_por_vez_no_mesmo_projeto()
    {
        var service = await Service();
        _runtime.Gate = new TaskCompletionSource();

        var first = service.PlanAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);
        var second = await Assert.ThrowsAsync<EngineException>(() => service.ValidateAsync(_projectId, IacOperationService.Person, CancellationToken.None));
        _runtime.Gate.SetResult();
        await first;

        Assert.Equal("IAC_OPERATION_RUNNING", second.Code);
    }

    [Fact]
    public async Task Plano_que_falha_nao_deixa_arquivo_e_fica_no_historico_com_o_erro()
    {
        _runtime.PlanError = "init falhou (backend ou providers): sem credencial";
        var service = await Service();

        var operation = await service.PlanAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationStatus.Failed, operation.Status);
        Assert.Null(operation.PlanId);
        Assert.Contains("sem credencial", operation.Error);
        Assert.False(Directory.Exists(Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", operation.Id)));
        Assert.Equal(operation.Id, service.Detail(_projectId, operation.Id).Id);
    }

    [Fact]
    public async Task Sem_ferramenta_disponivel_nada_e_instalado_nem_executado()
    {
        var workspace = Workspace();
        var runtimes = new IacRuntimeService([new OpenTofuToolchain(_project.DataDir, new MapLocator([]), new FakeOpenTofu(), new IacPlatform("windows", "amd64"))],
            new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), Runner(), workspace, _clock, _audit);
        var service = new IacOperationService(runtimes, new FakeFactory(_runtime), new IacConfigReader(), new FileIacWorkArea(_project.DataDir), workspace, null, _clock, _audit);

        var error = await Assert.ThrowsAsync<EngineException>(() => service.ValidateAsync(_projectId, IacOperationService.Person, CancellationToken.None));

        Assert.Equal("IAC_TERRAFORM_UNAVAILABLE", error.Code);
        Assert.False(_runtime.SawConfig);
    }

    private sealed class FakeFactory(FakeRuntime runtime) : IIacRuntimeFactory
    {
        public IIacRuntime For(IacInstallation installation) => runtime;
    }

    private sealed class FakeRuntime : IIacRuntime
    {
        public IReadOnlyList<string> Unformatted { get; set; } = [];
        public string? PlanError { get; set; }
        public TaskCompletionSource? Gate { get; set; }
        public IacPlanSpec? LastPlan { get; private set; }
        public bool SawConfig { get; private set; }
        public bool SawState { get; private set; }
        public bool SawDotTerraform { get; private set; }

        public IacEngine Engine => IacEngine.OpenTofu;
        public string Version => "1.13.9";

        public Task<IacFormatResult> FormatCheckAsync(string directory, CancellationToken ct)
        {
            Look(directory);
            return Task.FromResult(new IacFormatResult(true, Unformatted, null));
        }

        public Task<IacFolderValidation> ValidateAsync(string directory, string folder, string dataDirectory, CancellationToken ct) =>
            Task.FromResult(new IacFolderValidation(folder, true, true, [], null));

        public async Task<IacPlanRun> PlanAsync(IacPlanSpec spec, CancellationToken ct)
        {
            LastPlan = spec;
            Look(spec.Directory);
            if (Gate is not null) await Gate.Task;
            Directory.CreateDirectory(spec.DataDirectory);
            if (PlanError is not null) return new IacPlanRun(false, false, null, [], PlanError);
            File.WriteAllText(spec.PlanFile, "plano binário de mentira");
            return new IacPlanRun(true, true, new IacPlanSummary(1, 0, 0, 0, 0, 0, [new IacResourceChange("terraform_data.q", "terraform_data", "create")], []), [], null);
        }

        private void Look(string directory)
        {
            SawConfig = File.Exists(Path.Combine(directory, "resource.tf")) && File.Exists(Path.Combine(directory, "modules", "sqs", "policy.json"));
            SawState |= File.Exists(Path.Combine(directory, "terraform.tfstate"));
            SawDotTerraform |= Directory.Exists(Path.Combine(directory, ".terraform"));
        }
    }

    private sealed class MapLocator(Dictionary<string, string> map) : IExecutableLocator
    {
        public string? Find(string name) => map.GetValueOrDefault(name);
    }
}
