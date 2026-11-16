using System.Text.Json.Nodes;
using Mariliz.Application.Execution;
using Mariliz.Application.Iac;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Mariliz.Application.Protocol;
using Mariliz.Application.Tests.Support;
using Mariliz.Application.Workspace;
using Mariliz.Infrastructure;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests;

// Entrega 5: o desenho conferido vira Terraform no formato do modelo, revisado como diff e gravado como execução
// reversível. A IA só propõe componentes; ids da conta vêm dos fatos. O validate de verdade fica no teste ao vivo.
public sealed class IacGenerationTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly TempProject _project = TempProject.Dotnet();
    private readonly WorkspaceAuthorizer _workspace;
    private readonly FileRunStore _store;
    private readonly MemoryAudit _audit = new();
    private readonly ScriptedChat _chat = new();
    private readonly string _projectId;

    public IacGenerationTests()
    {
        _workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(_project.DataDir));
        _store = new FileRunStore(_project.DataDir);
        _projectId = _workspace.Authorize(_project.Root).Project!.ProjectId;
    }

    public void Dispose() => _project.Dispose();

    private IacGenerationService Service(bool model = true) =>
        new(_workspace, new IacConfigReader(), model ? new ModelGateway(new Settings(), new NoTransport(), chat: _chat) : null,
            new TestFilesFactory(), _store, new FakeClock { UtcNow = Now }, new RunTracker(), _audit);

    private static string Find(IReadOnlyList<IacAwsGenerator.File> files, string path) => files.Single(f => f.Path == path).Content;

    [Fact]
    public void Desenho_completo_gera_a_raiz_e_um_modulo_por_servico_no_formato_do_modelo()
    {
        var files = IacAwsGenerator.Generate(IacBlueprintSamples.Full(), "infra");

        Assert.Equal(
        [
            "infra/README.md", "infra/modules/alb/outputs.tf", "infra/modules/alb/resource.tf", "infra/modules/alb/variables.tf",
            "infra/modules/cloudwatch/resource.tf", "infra/modules/cloudwatch/variables.tf",
            "infra/modules/ecr/outputs.tf", "infra/modules/ecr/resource.tf", "infra/modules/ecr/variables.tf",
            "infra/modules/ecs/outputs.tf", "infra/modules/ecs/resource.tf", "infra/modules/ecs/variables.tf",
            "infra/modules/iam/outputs.tf", "infra/modules/iam/resource.tf", "infra/modules/iam/variables.tf",
            "infra/modules/rds/outputs.tf", "infra/modules/rds/resource.tf", "infra/modules/rds/variables.tf",
            "infra/modules/secrets/outputs.tf", "infra/modules/secrets/resource.tf", "infra/modules/secrets/variables.tf",
            "infra/modules/sns/outputs.tf", "infra/modules/sns/resource.tf", "infra/modules/sns/variables.tf",
            "infra/modules/sqs/outputs.tf", "infra/modules/sqs/resource.tf", "infra/modules/sqs/variables.tf",
            "infra/output.tf", "infra/providers.tf", "infra/resource.tf", "infra/terraform.tfvars.example", "infra/variables.tf"
        ], files.Select(f => f.Path).Order(StringComparer.Ordinal));

        var providers = Find(files, "infra/providers.tf");
        Assert.Contains($"version = \"{IacBootstrapTemplates.AwsVersion}\"", providers);
        Assert.Contains("required_version = \">= 1.11\"", providers);
        Assert.Contains("workspace_key_prefix = \"loja-api\"", providers);
        Assert.Contains("use_lockfile         = true", providers);
        Assert.Contains("allowed_account_ids = [\"123456789012\"]", providers);

        var root = Find(files, "infra/resource.tf");
        Assert.Contains("resource \"terraform_data\" \"environment_guard\"", root);
        Assert.Contains("condition     = terraform.workspace != \"default\"", root);
        Assert.Contains("name_prefix = \"loja-api-${terraform.workspace}\"", root);
        Assert.Contains("flows       = [\"pedidos\", \"estoque\"]", root);
        Assert.Contains("QUEUE_PEDIDOS_URL", root);
        Assert.Contains("DB_PASSWORD = \"${module.rds.master_secret_arn}:password::\"", root);

        var rds = Find(files, "infra/modules/rds/resource.tf");
        Assert.Contains("manage_master_user_password = true", rds);
        Assert.Contains("publicly_accessible         = false", rds);
        Assert.Contains("deletion_protection       = true", rds);
        Assert.DoesNotMatch(@"(?m)^\s*(master_)?password\s*=", rds);

        Assert.Contains("image_tag_mutability = \"IMMUTABLE\"", Find(files, "infra/modules/ecr/resource.tf"));
        Assert.Contains("ignore_changes = [task_definition]", Find(files, "infra/modules/ecs/resource.tf"));
        Assert.Contains("ssl_policy        = \"ELBSecurityPolicy-TLS13-1-2-2021-06\"", Find(files, "infra/modules/alb/resource.tf"));
        Assert.DoesNotContain("port              = \"80\"", Find(files, "infra/modules/alb/resource.tf"));
        Assert.Contains("resource \"aws_sqs_queue_redrive_allow_policy\" \"dlq\"", Find(files, "infra/modules/sqs/resource.tf"));
        Assert.DoesNotContain(((char)0x2014).ToString(), string.Concat(files.Select(f => f.Content)));
        Assert.All(files, f => Assert.DoesNotContain("\r", f.Content));
    }

    [Fact]
    public void So_filas_nao_pede_rede_nem_gera_servico_ou_banco()
    {
        var blueprint = IacBlueprintSamples.QueuesOnly();
        var files = IacAwsGenerator.Generate(blueprint, "infra");

        Assert.All(IacBlueprintChecks.Check(blueprint), check => Assert.True(check.Passed, check.Id));
        Assert.DoesNotContain(files, f => f.Path.Contains("/ecs/") || f.Path.Contains("/rds/") || f.Path.Contains("/alb/"));
        Assert.DoesNotContain("vpc_id", Find(files, "infra/variables.tf"));
        Assert.Contains("Sem backend remoto", Find(files, "infra/providers.tf"));
        Assert.DoesNotContain("allowed_account_ids", Find(files, "infra/providers.tf"));
        Assert.DoesNotContain(IacAwsGenerator.Costs(blueprint), c => c.Resource.Contains("Fargate"));
    }

    [Fact]
    public void Custos_listam_o_que_e_cobrado_sem_inventar_valor()
    {
        var costs = IacAwsGenerator.Costs(IacBlueprintSamples.Full());

        Assert.Contains(costs, c => c.Resource.StartsWith("RDS postgres db.t4g.micro", StringComparison.Ordinal));
        Assert.Contains(costs, c => c.Resource.Contains("NAT Gateway"));
        Assert.DoesNotContain(costs, c => c.Billing.Contains('$') || c.Billing.Contains("R$"));
    }

    [Theory]
    [InlineData("project", "Loja_API")]
    [InlineData("project", "um-nome-de-projeto-longo-demais")]
    [InlineData("certificate", "arn:aws:acm:us-east-1:123456789012:certificate/0f1e2d3c-4b5a-6978-8a9b-0c1d2e3f4a5b")]
    [InlineData("service-size", "256/4096")]
    [InlineData("load-balancer-subnets", "1")]
    [InlineData("database-subnets", "1")]
    [InlineData("queues", "Fila Com Espaco")]
    [InlineData("alarm-email", "sem-arroba")]
    public void Checagem_reprova_o_desenho_fora_do_modelo(string check, string value)
    {
        var full = IacBlueprintSamples.Full();
        var blueprint = check switch
        {
            "project" => full with { Project = value },
            "certificate" => full with { Service = full.Service! with { CertificateArn = value } },
            "service-size" => full with { Service = full.Service! with { Cpu = 256, Memory = 4096 } },
            "load-balancer-subnets" => full with { Network = full.Network with { PublicSubnets = ["subnet-0ccccccccccccccc3"] } },
            "database-subnets" => full with { Network = full.Network with { PrivateSubnets = ["subnet-0aaaaaaaaaaaaaaa1"] } },
            "queues" => full with { Queues = [value] },
            _ => full with { AlarmEmail = value }
        };

        var failed = IacBlueprintChecks.Check(blueprint).Where(c => !c.Passed).Select(c => c.Id).ToList();

        Assert.Equal([check], failed);
    }

    [Fact]
    public void Json_do_desenho_aceita_campos_faltando_sem_lancar()
    {
        var blueprint = IacBlueprintJson.Blueprint(JsonNode.Parse("""
            {"project": "  loja ", "region": "sa-east-1", "service": {"name": "api", "port": "8080", "cpu": 512.0, "memory": 1024},
             "queues": ["pedidos", 3, ""], "network": {"vpcId": 7}, "backend": {"region": "sa-east-1"}}
            """)!.AsObject());

        Assert.Equal("aws", blueprint.Provider);
        Assert.Equal("loja", blueprint.Project);
        Assert.Equal(8080, blueprint.Service!.Port);
        Assert.Equal(512, blueprint.Service.Cpu);
        Assert.Equal("/health", blueprint.Service.HealthPath);
        Assert.Equal(["pedidos"], blueprint.Queues);
        Assert.Equal("", blueprint.Network.VpcId);
        Assert.Null(blueprint.Backend);
        Assert.Contains(IacBlueprintChecks.Check(blueprint), c => c.Id == "vpc" && !c.Passed);
    }

    [Fact]
    public async Task Geracao_grava_so_com_o_plano_revisado_e_vira_execucao_reversivel()
    {
        _project.Write("infra/bootstrap/providers.tf", "provider \"aws\" {\n  region = \"sa-east-1\"\n}\n");
        var service = Service();
        var request = new IacGenerateRequest(_projectId, IacBlueprintSamples.Full(), null);

        var plan = service.Plan(request);

        Assert.Equal("infra", plan.Root);
        Assert.All(plan.Checks, c => Assert.True(c.Passed, c.Id));
        Assert.StartsWith("iacgen-", plan.PlanId);
        Assert.Contains(plan.Changes, c => c.Path == ".gitignore" && c.Diff.Contains("terraform.tfvars"));
        Assert.Contains(plan.Notes, n => n.Contains("NAT gateway"));
        Assert.NotEmpty(plan.Costs);
        var stale = Assert.Throws<EngineException>(() => service.StartSave(request with { Blueprint = request.Blueprint with { Queues = ["pedidos"] } }, plan.PlanId));
        Assert.Equal("STALE_PLAN", stale.Code);

        var run = service.StartSave(request, plan.PlanId);
        await run.Completion!;

        Assert.Contains("module \"rds\"", File.ReadAllText(Path.Combine(_project.Root, "infra", "resource.tf")));
        Assert.True(File.Exists(Path.Combine(_project.Root, "infra", "README.md")));
        Assert.True(File.Exists(Path.Combine(_project.Root, "infra", "terraform.tfvars.example")));
        Assert.True(File.Exists(Path.Combine(_project.Root, "infra", "bootstrap", "providers.tf")));
        Assert.Contains(_audit.Entries, e => e.Action == "iac.generated" && e.Detail.Contains("\"folder\":\"infra\""));

        var revert = new RunHandle("run-reverte0005", _projectId, RunKinds.Revert, (_, _, _, _, _) => { });
        var result = new RevertService(_store, new FakeClock { UtcNow = Now }).Execute(revert, new TestFilesFactory().Open(_project.Root), run.RunId, CancellationToken.None);
        Assert.True(result["verified"]!.GetValue<bool>());
        Assert.False(File.Exists(Path.Combine(_project.Root, "infra", "resource.tf")));
        Assert.True(File.Exists(Path.Combine(_project.Root, "infra", "bootstrap", "providers.tf")));
    }

    [Fact]
    public void Desenho_reprovado_volta_as_checagens_sem_arquivo_e_nao_grava()
    {
        var service = Service();
        var request = new IacGenerateRequest(_projectId, IacBlueprintSamples.Full() with { Region = "brasil" }, null);

        var plan = service.Plan(request);

        Assert.Empty(plan.Changes);
        Assert.Equal("", plan.PlanId);
        Assert.Contains(plan.Checks, c => c.Id == "region" && !c.Passed);
        Assert.Equal("IAC_BLUEPRINT_INVALID", Assert.Throws<EngineException>(() => service.StartSave(request, "")).Code);
    }

    [Theory]
    [InlineData("../fora")]
    [InlineData("C:/fora")]
    [InlineData("infra/.terraform")]
    [InlineData("pasta com espaco")]
    public void Pasta_fora_do_projeto_e_recusada(string folder) =>
        Assert.Equal("IAC_GENERATION_FOLDER_INVALID",
            Assert.Throws<EngineException>(() => Service().Plan(new IacGenerateRequest(_projectId, IacBlueprintSamples.Full(), folder))).Code);

    [Fact]
    public void Arquivo_que_nao_e_do_modelo_aparece_nas_notas()
    {
        _project.Write("infra/main.tf", "resource \"terraform_data\" \"antigo\" {}\n");

        var plan = Service().Plan(new IacGenerateRequest(_projectId, IacBlueprintSamples.QueuesOnly(), null));

        Assert.Contains(plan.Notes, n => n.Contains("infra/main.tf"));
    }

    [Fact]
    public async Task IA_propoe_componentes_e_os_fatos_da_conta_valem_sobre_o_que_ela_escreveu()
    {
        _chat.Replies.Enqueue("""
            {"blueprint": {"project": "loja-api", "region": "us-east-1", "account": "999999999999",
              "service": {"name": "api", "port": 8080, "healthPath": "/health", "cpu": 512, "memory": 1024, "desiredCount": 2, "publicIp": false,
                          "certificateArn": "arn:aws:acm:sa-east-1:999999999999:certificate/inventado"},
              "database": {"engine": "postgres", "engineVersion": "16", "instanceClass": "db.t4g.micro", "storageGb": 20, "multiAz": false},
              "queues": ["pedidos"], "network": {"vpcId": "vpc-0inventada000000"}},
             "notes": {"services": [{"name": "ECS Fargate", "reason": "Contêiner sem servidor para gerenciar."}], "risks": ["Custo do NAT"],
                       "alternatives": ["Lambda"], "assumptions": ["Tráfego baixo"]}}
            """);
        var facts = IacBlueprintSamples.Full() with { Project = "", Database = null, Queues = [] };

        var draft = await Service().DraftAsync(new IacDraftRequest(_projectId, "Uma API de pedidos com banco relacional e uma fila de pedidos.", facts), CancellationToken.None);

        Assert.True(draft.Valid);
        Assert.Equal(1, draft.Attempts);
        Assert.Equal("sa-east-1", draft.Blueprint!.Region);
        Assert.Equal("123456789012", draft.Blueprint.Account);
        Assert.Equal(facts.Network, draft.Blueprint.Network);
        Assert.Equal(IacBlueprintSamples.Certificate, draft.Blueprint.Service!.CertificateArn);
        Assert.Equal(2, draft.Blueprint.Service.DesiredCount);
        Assert.Equal(["ECS Fargate"], draft.Notes!.Services.Select(s => s.Name));
        var sent = _chat.Seen.Single().Messages[1].Content;
        Assert.DoesNotContain("vpc-0a1b2c3d4e5f60718", sent);
        Assert.DoesNotContain("123456789012", sent);
        Assert.Contains(_audit.Entries, e => e.Action == "iac.architecture_drafted");
    }

    [Fact]
    public async Task IA_tenta_de_novo_so_quando_erra_o_que_e_dela()
    {
        _chat.Replies.Enqueue("""{"blueprint": {"project": "loja", "region": "sa-east-1", "service": {"name": "api", "port": 80, "cpu": 256, "memory": 4096, "desiredCount": 1}}}""");
        _chat.Replies.Enqueue("""{"blueprint": {"project": "loja", "region": "sa-east-1", "service": {"name": "api", "port": 8080, "cpu": 256, "memory": 512, "desiredCount": 1}}}""");
        var noFacts = new IacBlueprint("aws", "", "", null, null, new IacNetwork("", [], []), null, null, [], null, null);

        var draft = await Service().DraftAsync(new IacDraftRequest(_projectId, "Uma API simples de cadastro de produtos da loja.", noFacts), CancellationToken.None);

        Assert.Equal(2, draft.Attempts);
        Assert.Contains("CPU e memória", _chat.Seen[1].Messages[1].Content);
        Assert.False(draft.Valid);
        Assert.All(draft.Checks.Where(c => !c.Passed), c => Assert.Contains(c.Id, new[] { "vpc", "private-subnets", "load-balancer-subnets", "certificate", "region" }));
    }

    [Fact]
    public async Task Sem_modelo_configurado_o_desenho_e_montado_a_mao()
    {
        var error = await Assert.ThrowsAsync<EngineException>(() =>
            Service(model: false).DraftAsync(new IacDraftRequest(_projectId, "Uma API simples de cadastro de produtos.", IacBlueprintSamples.Full()), CancellationToken.None));

        Assert.Equal("MODEL_NOT_CONFIGURED", error.Code);
    }

    private sealed class Settings : IModelSettings
    {
        public ModelStatus Status() => new(true, "openai");
        public void Save(string provider, string key) { }
        public void Clear() { }
        public bool TryGetKey(out string key)
        {
            key = "sk-chave-de-teste-iac-0000000000001";
            return true;
        }
    }

    private sealed class ScriptedChat : IModelChatTransport
    {
        public Queue<string> Replies { get; } = new();
        public List<ChatRequest> Seen { get; } = [];

        public Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(new ChatTransportResult(Replies.Count > 0 ? Replies.Dequeue() : "{}", "gpt-4o-mini-2024-07-18", 900, 300));
        }
    }

    private sealed class NoTransport : IModelTransport
    {
        public Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct) =>
            throw new InvalidOperationException();
    }
}
