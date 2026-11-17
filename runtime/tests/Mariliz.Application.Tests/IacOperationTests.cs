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
    public async Task Drift_le_a_copia_do_state_registra_so_nomes_e_nao_guarda_o_plano()
    {
        var before = _project.Fingerprint();
        var service = await Service();

        var operation = await service.DriftAsync(new IacPlanRequest(_projectId, null, "dev", null), IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationKind.Drift, operation.Kind);
        Assert.Equal(IacOperationStatus.Succeeded, operation.Status);
        Assert.True(operation.HasChanges);
        Assert.Null(operation.PlanId);
        Assert.Equal("dev", _runtime.LastDrift!.Workspace);
        Assert.True(_runtime.SawState);
        Assert.Equal(["changed", "deleted"], operation.Drift!.Select(d => d.Change));
        Assert.False(Directory.Exists(Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", operation.Id)));
        Assert.Equal(before, _project.Fingerprint());
        Assert.DoesNotContain(_audit.Entries, e => e.Detail.Contains(StateSecret));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.drift" && e.Detail.Contains("\"changed\":1") && e.Detail.Contains("\"deleted\":1"));
        Assert.Equal(IacOperationKind.Drift, service.History(_projectId)[0].Kind);

        var tools = new Mariliz.Application.Mcp.IacMcpTools(Workspace, _ => throw new InvalidOperationException(), _ => service, _project.Root, McpScope.Of(_projectId));
        var summary = await tools.Tools().Single(t => t.Info.Name == Mariliz.Application.Mcp.IacMcpTools.DriftTool)
            .Call(new Mariliz.Application.Mcp.McpCall(new System.Text.Json.Nodes.JsonObject(), "teste", CancellationToken.None));
        Assert.Contains("alterado fora da ferramenta: aws_sqs_queue.main[\"pedidos\"] (tags, visibility_timeout_seconds)", summary);
        Assert.Contains("apagado fora da ferramenta: aws_sns_topic.alerts", summary);
        Assert.DoesNotContain(StateSecret, summary);
    }

    [Fact]
    public async Task Drift_que_falha_guarda_o_erro_e_a_trava_sem_lista()
    {
        _runtime.DriftResult = new IacDriftRun(false, [], "Error acquiring the state lock\nLock Info:\n  ID:        6f1c2a9e-1b2c-4d5e-8f90-123456789abc");
        var operation = await (await Service()).DriftAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);

        Assert.Equal(IacOperationStatus.Failed, operation.Status);
        Assert.Null(operation.Drift);
        Assert.Null(operation.HasChanges);
        Assert.Equal("6f1c2a9e-1b2c-4d5e-8f90-123456789abc", operation.LockId);
        Assert.Equal("default", operation.Workspace);
    }

    [Fact]
    public async Task Custo_do_plano_soma_antes_e_depois_e_deixa_fora_o_que_nao_da_para_cotar()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(IacDriftAndCostTests.PlanJson)!;
        _runtime.Billables = IacPlanBillables.Read(json);
        _runtime.Summary = IacPlanReader.Read(json).Summary;
        var provider = new PricedAwsProvider();
        var cloud = new CloudHub(new FileCloudStore(_project.DataDir), new CloudProviderRegistry([provider]), Workspace(), _clock, _audit);
        var connection = await cloud.Connections.CreateAsync("aws", "Conta de teste", "simple", new Dictionary<string, string> { ["region"] = "sa-east-1" },
            CancellationToken.None);
        cloud.Projects.Link(_projectId, connection.Id, [CloudReadService.PricingCapability("aws")], agentAccess: false);
        var service = await Service(cloud);
        var plan = await service.PlanAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);
        var costs = new IacCostService(Workspace(), cloud, _clock, service);

        var estimate = await costs.EstimatePlanAsync(new IacPlanCostRequest(_projectId, plan.Id, connection.Id, null), CloudCaller.Person,
            CancellationToken.None);

        Assert.Equal(plan.PlanId, estimate.PlanId);
        Assert.Equal(97.09m, estimate.MonthlyBefore);
        Assert.Equal(145.12m, estimate.MonthlyAfter);
        Assert.Equal(48.03m, estimate.MonthlyDelta);
        Assert.False(estimate.Complete);
        Assert.Equal(67.89m, estimate.Lines.Single(l => l.Address == "aws_nat_gateway.main").Before);
        Assert.Equal(61.90m, estimate.Lines.Single(l => l.Type == "aws_ecs_service").After);
        var legacy = estimate.Lines.Single(l => l.Address == "aws_db_instance.legacy");
        Assert.Equal((29.20m, 54.02m), (legacy.Before!.Value, legacy.After!.Value));
        var unknown = estimate.Lines.Single(l => l.Address == "aws_db_instance.novo");
        Assert.Null(unknown.After);
        Assert.Contains("storage_type", unknown.Problem);
        Assert.Equal(["aws_ecs_task_definition", "aws_sqs_queue"], estimate.Uncovered);
        Assert.Contains(_audit.Entries, e => e.Action == "cloud.read" && e.Detail.Contains("aws.pricing"));
        Assert.DoesNotContain(provider.Queries, q => q.Attributes.Values.Contains("senha-do-plano"));

        var tools = new Mariliz.Application.Mcp.IacMcpTools(Workspace, _ => throw new InvalidOperationException(), _ => service, _project.Root, McpScope.Of(_projectId));
        var summary = await tools.Tools().Single(t => t.Info.Name == Mariliz.Application.Mcp.IacMcpTools.PlanSummaryTool)
            .Call(new Mariliz.Application.Mcp.McpCall(new System.Text.Json.Nodes.JsonObject(), "teste", CancellationToken.None));
        Assert.Contains("Recursos com custo fixo que o plano muda: create RDS postgres db.t4g.micro", summary);
        Assert.DoesNotContain("senha-do-plano", summary);
    }

    [Fact]
    public async Task Custo_do_plano_recusa_plano_antigo_e_o_que_nao_e_plano()
    {
        _runtime.Billables = null;
        var service = await Service();
        var old = await service.PlanAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);
        var validation = await service.ValidateAsync(_projectId, IacOperationService.Person, CancellationToken.None);
        var costs = new IacCostService(Workspace(), null, _clock, service);

        var withoutData = await Assert.ThrowsAsync<EngineException>(() =>
            costs.EstimatePlanAsync(new IacPlanCostRequest(_projectId, old.Id, "cloud-x", null), CloudCaller.Person, CancellationToken.None));
        var notPlan = await Assert.ThrowsAsync<EngineException>(() =>
            costs.EstimatePlanAsync(new IacPlanCostRequest(_projectId, validation.Id, "cloud-x", null), CloudCaller.Person, CancellationToken.None));

        Assert.Equal("IAC_PLAN_COST_UNAVAILABLE", withoutData.Code);
        Assert.Equal(IacErrors.NotAPlan().Code, notPlan.Code);
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

    // P0-1: com AssumeRole, a ferramenta recebia só o perfil de origem e rodava com a identidade dele, não com a do papel.
    private sealed class RoleProvider : ICloudProvider, ICloudProcessCredentials
    {
        public const string SecretKey = "segredo-temporario-do-papel-7f3a";

        public int Issued { get; private set; }
        public EngineException? Failure { get; set; }

        public CloudProviderDescriptor Descriptor { get; } = new("aws", "AWS de teste", "1.0.0",
            [new CloudMechanism("assume-role", "Papel", "Teste.", [new CloudField("roleArn", "Role ARN", "", Required: true),
                new CloudField("sourceProfile", "Perfil de origem", "", Required: false), new CloudField("region", "Região", "", Required: true)])],
            [], "Teste.");

        public CloudSettings Normalize(CloudSettings settings) => settings;

        public string NormalizeRegion(string region) => region;

        public Task<CloudIdentity> IdentifyAsync(CloudSettings settings, CancellationToken ct) =>
            Task.FromResult(new CloudIdentity("123456789012", null, "arn:teste", null));

        public Task<CloudTable> ReadAsync(CloudSettings settings, CloudQuery query, CancellationToken ct) => Task.FromResult(new CloudTable([], false));

        public Task<IReadOnlyDictionary<string, string>> ProcessEnvironmentAsync(CloudSettings settings, CancellationToken ct)
        {
            if (Failure is not null) throw Failure;
            Issued++;
            IReadOnlyDictionary<string, string> environment = new Dictionary<string, string>
            {
                ["AWS_ACCESS_KEY_ID"] = "ASIATESTE" + Issued, ["AWS_SECRET_ACCESS_KEY"] = SecretKey, ["AWS_SESSION_TOKEN"] = "sessao-" + Issued
            };
            return Task.FromResult(environment);
        }
    }

    private async Task<(IacOperationService Service, RoleProvider Provider)> WithRoleConnection()
    {
        var store = new FileCloudStore(_project.DataDir);
        store.SaveConnection(new CloudConnection("c-papel", "aws", "Papel", new CloudSettings("assume-role", new Dictionary<string, string>
            {
                ["roleArn"] = "arn:aws:iam::123456789012:role/deploy", ["sourceProfile"] = "origem-sso", ["region"] = "sa-east-1"
            }), CloudConnectionStatus.Connected, null, [], _clock.UtcNow, _clock.UtcNow, null, "1"));
        store.SaveLink(new CloudProjectLink("l-papel", _projectId, "c-papel", "aws", [], false, _clock.UtcNow));
        var provider = new RoleProvider();
        return (await Service(new CloudHub(store, new CloudProviderRegistry([provider]), Workspace(), _clock, _audit)), provider);
    }

    [Fact]
    public async Task Com_papel_assumido_a_ferramenta_roda_com_a_credencial_do_papel_e_nao_com_o_perfil_de_origem()
    {
        var (service, provider) = await WithRoleConnection();

        var plan = await service.PlanAsync(new IacPlanRequest(_projectId, null, null, "c-papel"), IacOperationService.Person, CancellationToken.None);
        await service.ApplyAsync(Approve(plan), CancellationToken.None);
        await service.DriftAsync(new IacPlanRequest(_projectId, null, null, "c-papel"), IacOperationService.Person, CancellationToken.None);

        foreach (var environment in new[] { _runtime.LastPlan!.Environment, _runtime.LastApply!.Environment, _runtime.LastDrift!.Environment })
        {
            Assert.Equal(RoleProvider.SecretKey, environment["AWS_SECRET_ACCESS_KEY"]);
            Assert.StartsWith("sessao-", environment["AWS_SESSION_TOKEN"]);
            Assert.False(environment.ContainsKey("AWS_PROFILE"));
            Assert.Equal("sa-east-1", environment["AWS_REGION"]);
        }
        // Uma credencial por operação, pedida na hora: o apply não reusa a do plano.
        Assert.Equal(3, provider.Issued);
        Assert.NotEqual(_runtime.LastPlan.Environment["AWS_SESSION_TOKEN"], _runtime.LastApply.Environment["AWS_SESSION_TOKEN"]);
        Assert.DoesNotContain(_audit.Entries, entry => entry.Detail.Contains(RoleProvider.SecretKey));
        Assert.DoesNotContain(Directory.EnumerateFiles(_project.DataDir, "*", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains(RoleProvider.SecretKey));
    }

    [Fact]
    public async Task Sem_credencial_do_papel_a_ferramenta_nem_roda()
    {
        var (service, provider) = await WithRoleConnection();
        provider.Failure = CloudErrors.Expired("Rode aws sso login --profile origem-sso e valide de novo.");

        var refused = await Assert.ThrowsAsync<EngineException>(() =>
            service.PlanAsync(new IacPlanRequest(_projectId, null, null, "c-papel"), IacOperationService.Person, CancellationToken.None));

        Assert.StartsWith(CloudErrors.Prefix, refused.Code);
        Assert.Null(_runtime.LastPlan);
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

    private async Task<(IacOperationService Service, IacOperation Plan)> Planned(string? workspace = null)
    {
        var service = await Service();
        var plan = await service.PlanAsync(new IacPlanRequest(_projectId, null, workspace, null), IacOperationService.Person, CancellationToken.None);
        return (service, plan);
    }

    private static IacApplyRequest Approve(IacOperation plan, bool destroy = false, bool risks = false, string? workspace = null) =>
        new(plan.ProjectId, plan.Id, plan.PlanId!, destroy, risks, workspace);

    [Fact]
    public async Task Aplicar_usa_o_arquivo_do_plano_aprovado_verifica_e_consome_o_plano()
    {
        var (service, plan) = await Planned("dev");

        var applied = await service.ApplyAsync(Approve(plan), CancellationToken.None);
        var again = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));

        Assert.Equal(IacOperationKind.Apply, applied.Kind);
        Assert.Equal(IacOperationStatus.Succeeded, applied.Status);
        Assert.True(applied.Apply!.Verified);
        Assert.Equal(3, applied.Apply.ManagedResources);
        Assert.Equal(plan.Id, applied.PlanOperationId);
        Assert.Equal(plan.PlanId, applied.Approval!.PlanId);
        Assert.True(_runtime.PlanFileExistedOnApply);
        Assert.Equal("dev", _runtime.LastApply!.Workspace);
        Assert.EndsWith("plan.tfplan", _runtime.LastApply.PlanFile);
        Assert.Equal(applied.Id, service.Detail(_projectId, plan.Id).AppliedBy);
        Assert.False(Directory.Exists(Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", plan.Id)));
        Assert.Equal("IAC_PLAN_ALREADY_APPLIED", again.Code);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.apply_approved" && e.Detail.Contains(plan.PlanId!));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.apply" && e.Detail.Contains("\"verified\":true"));
    }

    [Fact]
    public async Task Aprovacao_de_outro_plano_e_recusada()
    {
        var (service, plan) = await Planned();

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            service.ApplyAsync(Approve(plan) with { PlanId = "0000000000000000" }, CancellationToken.None));

        Assert.Equal("IAC_PLAN_MISMATCH", error.Code);
        Assert.Null(_runtime.LastApply);
    }

    [Fact]
    public async Task Configuracao_mudada_no_projeto_invalida_a_aprovacao()
    {
        var (service, plan) = await Planned();
        _project.Write("infra/modules/sqs/resource.tf", "resource \"terraform_data\" \"q\" {}\nresource \"terraform_data\" \"r\" {}\n");

        var error = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));

        Assert.Equal("IAC_PLAN_STALE", error.Code);
        Assert.Null(_runtime.LastApply);
    }

    [Fact]
    public async Task Copia_ou_arquivo_do_plano_alterados_nao_sao_aplicados()
    {
        var (service, plan) = await Planned();
        var directory = Path.Combine(_project.DataDir, "projects", _projectId, "iac", "operations", plan.Id);
        File.AppendAllText(Path.Combine(directory, "config", "resource.tf"), "\n# alterado depois do plano\n");

        var error = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));

        Assert.Equal("IAC_PLAN_CHANGED", error.Code);
        Assert.Null(_runtime.LastApply);
    }

    [Fact]
    public async Task Plano_mais_novo_ou_vencido_nao_e_aplicado()
    {
        var (service, first) = await Planned();
        await service.PlanAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);
        var superseded = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(first), CancellationToken.None));

        var latest = service.History(_projectId).First(o => o.Kind == IacOperationKind.Plan);
        _clock.UtcNow += TimeSpan.FromHours(25);
        var expired = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(latest), CancellationToken.None));

        Assert.Equal("IAC_PLAN_SUPERSEDED", superseded.Code);
        Assert.Equal("IAC_PLAN_EXPIRED", expired.Code);
        Assert.Null(_runtime.LastApply);
    }

    [Fact]
    public async Task Destruicao_pede_confirmacao_explicita_a_mais()
    {
        _runtime.Summary = new IacPlanSummary(0, 0, 1, 1, 0, 0,
            [new IacResourceChange("aws_db_instance.old", "aws_db_instance", "delete"), new IacResourceChange("aws_ecr_repository.api", "aws_ecr_repository", "replace")], []);
        var (service, plan) = await Planned();

        var refused = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));
        var applied = await service.ApplyAsync(Approve(plan, destroy: true), CancellationToken.None);

        Assert.Equal("IAC_DESTROY_CONFIRMATION_REQUIRED", refused.Code);
        Assert.Contains("2 recurso", refused.Message);
        Assert.True(applied.Approval!.DestroyConfirmed);
    }

    [Fact]
    public async Task Achado_grave_pede_que_a_pessoa_confirme_a_revisao()
    {
        _runtime.Findings = [new IacFinding(IacFindingSeverity.Danger, "open-ingress", "aws_security_group.tasks", "abre a porta 22")];
        var (service, plan) = await Planned();

        var refused = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));
        var applied = await service.ApplyAsync(Approve(plan, risks: true), CancellationToken.None);

        Assert.Equal("IAC_RISKS_CONFIRMATION_REQUIRED", refused.Code);
        Assert.True(applied.Approval!.RisksAccepted);
    }

    [Fact]
    public async Task Producao_pede_o_nome_do_ambiente_digitado()
    {
        var (service, plan) = await Planned("prd");

        var refused = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan, workspace: "prod"), CancellationToken.None));
        var applied = await service.ApplyAsync(Approve(plan, workspace: "prd"), CancellationToken.None);

        Assert.Equal("IAC_WORKSPACE_CONFIRMATION_REQUIRED", refused.Code);
        Assert.Equal("prd", applied.Approval!.WorkspaceConfirmed);
    }

    [Fact]
    public async Task Plano_com_state_dentro_da_copia_e_mostrado_mas_nao_aplicado()
    {
        _runtime.PlanBlocker = "state local dentro da pasta";
        var (service, plan) = await Planned();

        var error = await Assert.ThrowsAsync<EngineException>(() => service.ApplyAsync(Approve(plan), CancellationToken.None));

        Assert.Equal("state local dentro da pasta", plan.ApplyBlocker);
        Assert.Equal("IAC_APPLY_BLOCKED", error.Code);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.plan" && e.Detail.Contains("\"applicable\":false"));
    }

    [Fact]
    public async Task Falha_no_apply_registra_o_que_ficou_pendente_e_consome_o_plano()
    {
        _runtime.ApplyResult = new IacApplyRun(false, false,
            new IacPlanSummary(1, 0, 0, 0, 0, 0, [new IacResourceChange("terraform_data.q", "terraform_data", "create")], []), 2,
            new Dictionary<string, string>(), "Error: creating SQS Queue: AccessDenied");
        var (service, plan) = await Planned();

        var failed = await service.ApplyAsync(Approve(plan), CancellationToken.None);

        Assert.Equal(IacOperationStatus.Failed, failed.Status);
        Assert.Equal("terraform_data.q", Assert.Single(failed.Apply!.Remaining!.Changes).Address);
        Assert.Contains("AccessDenied", failed.Error);
        Assert.Equal(failed.Id, service.Detail(_projectId, plan.Id).AppliedBy);
    }

    [Fact]
    public async Task Plano_que_falhou_ou_sem_mudancas_nao_e_aplicavel()
    {
        _runtime.PlanError = "init falhou";
        var (service, failedPlan) = await Planned();

        var error = await Assert.ThrowsAsync<EngineException>(() =>
            service.ApplyAsync(new IacApplyRequest(_projectId, failedPlan.Id, "x", false, false, null), CancellationToken.None));

        Assert.Equal("IAC_NOT_A_PLAN", error.Code);
    }

    internal sealed class FakeFactory(FakeRuntime runtime) : IIacRuntimeFactory
    {
        public IIacRuntime For(IacInstallation installation) => runtime;
    }

    internal sealed class FakeRuntime : IIacRuntime
    {
        public IReadOnlyList<string> Unformatted { get; set; } = [];
        public string? PlanError { get; set; }
        public string? PlanBlocker { get; set; }
        public IacStateMode PlanStateMode { get; set; } = IacStateMode.Remote;
        public string? PlanLocalState { get; set; }
        // O que o apply grava no state da cópia (state local), e o que acontece no projeto enquanto ele roda.
        public string? ApplyWritesState { get; set; }
        public Action? DuringApply { get; set; }
        public IacStateRead StateResult { get; set; } = new(true, "default", ["default", "dev"], ["aws_s3_bucket.tf_state"], null);
        public string? UnlockError { get; set; }
        public (IacStateSpec Spec, string LockId)? LastUnlock { get; private set; }
        public IacPlanSummary Summary { get; set; } = new(1, 0, 0, 0, 0, 0, [new IacResourceChange("terraform_data.q", "terraform_data", "create")], []);
        public IReadOnlyList<IacFinding> Findings { get; set; } = [];
        public IReadOnlyList<IacBillable>? Billables { get; set; } = [];
        public IacApplyRun ApplyResult { get; set; } = new(true, true, null, 3, new Dictionary<string, string> { ["fila"] = "fila-dev" }, null);
        public IacApplySpec? LastApply { get; private set; }
        public bool PlanFileExistedOnApply { get; private set; }
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
            File.WriteAllText(spec.PlanFile, "plano binário de mentira " + Guid.NewGuid());
            return new IacPlanRun(true, true, Summary, Findings, null, PlanBlocker is null && PlanStateMode == IacStateMode.Remote ? "s3" : "local", PlanBlocker,
                PlanStateMode, PlanLocalState, Billables);
        }

        public Task<IacApplyRun> ApplyAsync(IacApplySpec spec, CancellationToken ct)
        {
            LastApply = spec;
            PlanFileExistedOnApply = File.Exists(spec.PlanFile);
            if (ApplyWritesState is not null && PlanLocalState is not null) File.WriteAllText(Path.Combine(spec.Directory, PlanLocalState), ApplyWritesState);
            DuringApply?.Invoke();
            return Task.FromResult(ApplyResult);
        }

        public Task<IacStateRead> StateAsync(IacStateSpec spec, CancellationToken ct) => Task.FromResult(StateResult);

        public IacDriftRun DriftResult { get; set; } = new(true, [new IacDriftedResource("aws_sqs_queue.main[\"pedidos\"]", "aws_sqs_queue", "changed",
            ["tags", "visibility_timeout_seconds"]), new IacDriftedResource("aws_sns_topic.alerts", "aws_sns_topic", "deleted", [])], null, "s3");
        public IacPlanSpec? LastDrift { get; private set; }

        public Task<IacDriftRun> DriftAsync(IacPlanSpec spec, CancellationToken ct)
        {
            LastDrift = spec;
            Look(spec.Directory);
            File.WriteAllText(spec.PlanFile, "plano refresh-only com valores " + StateSecret);
            return Task.FromResult(DriftResult);
        }

        public Task<string?> UnlockAsync(IacStateSpec spec, string lockId, CancellationToken ct)
        {
            LastUnlock = (spec, lockId);
            return Task.FromResult(UnlockError);
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
