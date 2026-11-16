using System.Text.Json.Nodes;
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

// Entrega 9: o que está na conta fora do state (só dos tipos que o state gerencia, só pelas leituras liberadas) e a
// verificação de drift agendada, com a ferramenta de mentira atrás da IIacRuntime.
public sealed class IacDriftScheduleTests : IDisposable
{
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly MemoryAudit _audit = new();
    private readonly FakeClock _clock = new();
    private readonly IacOperationTests.FakeRuntime _runtime = new();
    private readonly PricedAwsProvider _provider = new();
    private readonly Dictionary<string, string> _external = [];
    private readonly string _projectId;

    public IacDriftScheduleTests()
    {
        _projectId = Workspace().Authorize(_project.Root).Project!.ProjectId;
        _project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.11\"\n}\n");
        _project.Write("infra/resource.tf", "resource \"terraform_data\" \"q\" {}\n");
    }

    public void Dispose() => _project.Dispose();

    private WorkspaceAuthorizer Workspace() => new(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));

    private async Task<(IacOperationService Operations, CloudHub Cloud, string ConnectionId)> Setup(IReadOnlyList<string> capabilities)
    {
        var tofu = Path.Combine(_project.DataDir, "fora", "tofu.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(tofu)!);
        File.WriteAllText(tofu, "tofu de mentira");
        _external["tofu"] = tofu;
        var workspace = Workspace();
        var runner = new InterceptingRunner((_, _) => Task.FromResult<Mariliz.Application.Ports.ProcessResult?>(
            new Mariliz.Application.Ports.ProcessResult(0, """{"terraform_version":"1.13.9","platform":"windows_amd64"}""", "", false, false)));
        var runtimes = new IacRuntimeService([new OpenTofuToolchain(_project.DataDir, new MapLocator(_external), new FakeOpenTofu(), new IacPlatform("windows", "amd64"))],
            new FileIacSettingsStore(_project.DataDir), new IacConfigReader(), runner, workspace, _clock, _audit);
        await runtimes.AdoptAsync("1.13.9", IacEngine.OpenTofu, CancellationToken.None);
        var cloud = new CloudHub(new FileCloudStore(_project.DataDir), new CloudProviderRegistry([_provider]), workspace, _clock, _audit);
        var connection = await cloud.Connections.CreateAsync("aws", "Conta de teste", "simple", new Dictionary<string, string> { ["region"] = "sa-east-1" },
            CancellationToken.None);
        cloud.Projects.Link(_projectId, connection.Id, capabilities, agentAccess: false);
        var operations = new IacOperationService(runtimes, new IacOperationTests.FakeFactory(_runtime), new IacConfigReader(), new FileIacWorkArea(_project.DataDir),
            workspace, cloud, _clock, _audit);
        return (operations, cloud, connection.Id);
    }

    [Fact]
    public void Do_state_saem_so_os_identificadores_dos_tipos_comparados()
    {
        var plan = JsonNode.Parse("""
            { "prior_state": { "values": { "root_module": {
                "resources": [
                  { "address": "aws_vpc.main", "mode": "managed", "type": "aws_vpc", "values": { "id": "vpc-1", "cidr_block": "10.0.0.0/16" } },
                  { "address": "aws_iam_role.x", "mode": "managed", "type": "aws_iam_role", "values": { "id": "papel" } },
                  { "address": "data.aws_vpc.d", "mode": "data", "type": "aws_vpc", "values": { "id": "vpc-data" } }
                ],
                "child_modules": [ { "resources": [
                  { "address": "module.db.aws_db_instance.this", "mode": "managed", "type": "aws_db_instance",
                    "values": { "id": "db-ABCDEF", "identifier": "loja-db", "password": "segredo" } } ] } ]
            } } } }
            """)!;

        var ids = IacUnmanaged.StateIds(plan);

        Assert.Equal(["aws_db_instance", "aws_vpc"], ids.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["vpc-1"], ids["aws_vpc"]);
        Assert.Equal(["loja-db"], ids["aws_db_instance"]);
    }

    [Fact]
    public async Task Drift_lista_o_que_esta_na_conta_fora_do_state_so_pelas_leituras_liberadas()
    {
        var (operations, _, connectionId) = await Setup(["aws.ec2.vpcs", "aws.s3.buckets"]);
        _runtime.DriftResult = new IacDriftRun(true, [], null, "s3", new Dictionary<string, IReadOnlySet<string>>
        {
            ["aws_vpc"] = new HashSet<string> { "vpc-1" },
            ["aws_instance"] = new HashSet<string> { "i-1" },
            ["aws_s3_bucket"] = new HashSet<string> { "loja-state" }
        });
        _provider.Tables["aws.ec2.vpcs"] = [["vpc-1", "loja", "10.0.0.0/16", "não", "available"], ["vpc-2", "manual", "10.1.0.0/16", "não", "available"],
            ["vpc-default", null, "172.31.0.0/16", "sim", "available"]];
        _provider.Tables["aws.s3.buckets"] = [["loja-state", "2026-01-01", "sa-east-1"], ["esquecido", "2026-01-02", "sa-east-1"], ["de-fora", "2026-01-03", "us-east-1"]];

        var drift = await operations.DriftAsync(new IacPlanRequest(_projectId, null, null, connectionId), IacOperationService.Person, CancellationToken.None);

        Assert.Equal(["vpc-2", "esquecido"], drift.Unmanaged!.Select(u => u.Id));
        Assert.Equal("manual", drift.Unmanaged![0].Name);
        Assert.Contains(drift.UnmanagedNotes!, n => n.Contains("aws.ec2.instances") && n.Contains("não está liberada"));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.drift" && e.Detail.Contains("\"unmanaged\":2"));
        Assert.Contains(_audit.Entries, e => e.Action == "cloud.read" && e.Detail.Contains("aws.ec2.vpcs"));
    }

    [Fact]
    public async Task Sem_conexao_o_drift_nao_compara_com_a_conta()
    {
        var (operations, _, _) = await Setup(["aws.ec2.vpcs"]);
        _runtime.DriftResult = new IacDriftRun(true, [], null, "s3", new Dictionary<string, IReadOnlySet<string>> { ["aws_vpc"] = new HashSet<string> { "vpc-1" } });

        var drift = await operations.DriftAsync(new IacPlanRequest(_projectId, null, null, null), IacOperationService.Person, CancellationToken.None);

        Assert.Null(drift.Unmanaged);
        Assert.DoesNotContain(_audit.Entries, e => e.Action == "cloud.read");
    }

    [Fact]
    public async Task Agendamento_roda_ao_ligar_e_depois_so_quando_o_intervalo_vence()
    {
        var (operations, _, connectionId) = await Setup(["aws.ec2.vpcs"]);
        var scheduler = new IacDriftScheduler(operations, new FileIacDriftScheduleStore(_project.DataDir), Workspace(), _clock, _audit);

        var view = scheduler.Set(new IacDriftScheduleRequest(_projectId, true, 12, "infra", "dev", connectionId));
        var first = await scheduler.RunDueAsync(CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddHours(11);
        var early = await scheduler.RunDueAsync(CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddHours(2);
        var due = await scheduler.RunDueAsync(CancellationToken.None);

        Assert.Equal(_clock.UtcNow.AddHours(-13), view.NextRunAt);
        Assert.Equal((1, 0, 1), (first, early, due));
        var history = operations.History(_projectId);
        Assert.Equal(2, history.Count(o => o.Kind == IacOperationKind.Drift && o.RequestedBy == IacDriftScheduler.Requester));
        Assert.Equal("dev", _runtime.LastDrift!.Workspace);
        var saved = scheduler.Get(_projectId);
        Assert.Equal(history[0].Id, saved.Schedule!.LastOperationId);
        Assert.Equal(_clock.UtcNow.AddHours(12), saved.NextRunAt);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.drift_schedule_changed" && e.Detail.Contains("\"intervalHours\":12"));
    }

    [Fact]
    public async Task Agendamento_desligado_nao_roda_e_intervalo_ou_pasta_invalidos_sao_recusados()
    {
        var (operations, _, connectionId) = await Setup(["aws.ec2.vpcs"]);
        var scheduler = new IacDriftScheduler(operations, new FileIacDriftScheduleStore(_project.DataDir), Workspace(), _clock, _audit);

        var interval = Assert.Throws<EngineException>(() => scheduler.Set(new IacDriftScheduleRequest(_projectId, true, 5, null, null, null)));
        var folder = Assert.Throws<EngineException>(() => scheduler.Set(new IacDriftScheduleRequest(_projectId, true, 24, "outra", null, null)));
        var notLinked = Assert.Throws<EngineException>(() => scheduler.Set(new IacDriftScheduleRequest(_projectId, true, 24, null, null, "cloud-desconhecida")));
        scheduler.Set(new IacDriftScheduleRequest(_projectId, false, 24, null, null, connectionId));
        var ran = await scheduler.RunDueAsync(CancellationToken.None);

        Assert.Equal("IAC_SCHEDULE_INVALID", interval.Code);
        Assert.Equal(IacErrors.FolderUnknown("outra").Code, folder.Code);
        Assert.StartsWith("IAC_", notLinked.Code);
        Assert.Equal(0, ran);
        Assert.Null(scheduler.Get(_projectId).NextRunAt);
    }

    [Fact]
    public async Task Falha_do_agendamento_conta_como_rodada_e_fica_na_trilha()
    {
        var (operations, _, _) = await Setup(["aws.ec2.vpcs"]);
        var store = new FileIacDriftScheduleStore(_project.DataDir);
        var scheduler = new IacDriftScheduler(operations, store, Workspace(), _clock, _audit);
        scheduler.Set(new IacDriftScheduleRequest(_projectId, true, 6, null, null, null));
        _project.Write("infra/providers.tf", "");
        _project.Write("infra/resource.tf", "");
        File.Delete(Path.Combine(_project.Root, "infra", "providers.tf"));
        File.Delete(Path.Combine(_project.Root, "infra", "resource.tf"));

        var ran = await scheduler.RunDueAsync(CancellationToken.None);
        var again = await scheduler.RunDueAsync(CancellationToken.None);

        Assert.Equal((0, 0), (ran, again));
        Assert.NotNull(scheduler.Get(_projectId).Schedule!.LastError);
        Assert.Single(_audit.Entries, e => e.Action == "iac.drift_schedule_failed");
    }

    private sealed class MapLocator(Dictionary<string, string> map) : IExecutableLocator
    {
        public string? Find(string name) => map.GetValueOrDefault(name);
    }
}
