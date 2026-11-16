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

    // O provider AWS fixado no modelo autentica pelo mesmo perfil que a conexão usa (SSO, chaves ou aws login), recebendo só
    // AWS_PROFILE. O plano lê apenas aws_caller_identity: nada é criado.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS", "MARILIZ_LIVE_AWS_PROFILE")]
    public async Task Provider_aws_autentica_pelo_perfil_da_conexao()
    {
        using var project = TempProject.Dotnet();
        project.Write("infra/providers.tf", "terraform {\n  required_providers {\n    aws = {\n      source  = \"hashicorp/aws\"\n" +
                                            $"      version = \"{IacBootstrapTemplates.AwsVersion}\"\n    }}\n  }}\n}}\n\nprovider \"aws\" {{}}\n");
        project.Write("infra/resource.tf", "data \"aws_caller_identity\" \"current\" {}\n\noutput \"conta_lida\" {\n  value = length(data.aws_caller_identity.current.account_id) == 12\n}\n");
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        workspace.Authorize(project.Root);
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(),
            new MemoryAudit());
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        var status = await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var runtime = new CliIacRuntimeFactory(runner, project.DataDir).For(status.Selected!);
        var environment = new Dictionary<string, string>
        {
            ["AWS_PROFILE"] = Environment.GetEnvironmentVariable("MARILIZ_LIVE_AWS_PROFILE")!,
            ["AWS_REGION"] = "sa-east-1"
        };

        var plan = await runtime.PlanAsync(new IacPlanSpec(Path.Combine(project.Root, "infra"), Path.Combine(project.DataDir, "tf-data"),
            Path.Combine(project.DataDir, "plan.tfplan"), null, environment), CancellationToken.None);

        output.WriteLine($"plano: {(plan.Succeeded ? "ok" : plan.Error)}; outputs alterados: {string.Join(",", plan.Summary?.OutputsChanged ?? [])}");
        Assert.True(plan.Succeeded, plan.Error);
        Assert.Contains("conta_lida", plan.Summary!.OutputsChanged);
    }

    // Entrega 8 de ponta a ponta: o OpenTofu planeja recursos AWS cobrados com o perfil da conexão (só o plano: nada é aplicado
    // nem criado) e o Spock cota o plano com a Price List Query API real, pela mesma conexão.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS", "MARILIZ_LIVE_AWS_PROFILE")]
    public async Task Custo_de_um_plano_aws_real_com_precos_reais()
    {
        using var project = TempProject.Dotnet();
        project.Write("infra/providers.tf", "terraform {\n  required_providers {\n    aws = {\n      source  = \"hashicorp/aws\"\n" +
                                            $"      version = \"{IacBootstrapTemplates.AwsVersion}\"\n    }}\n  }}\n}}\n\nprovider \"aws\" {{}}\n");
        project.Write("infra/resource.tf", """
            resource "aws_instance" "web" {
              ami           = "ami-0123456789abcdef0"
              instance_type = "t3.micro"
            }

            resource "aws_db_instance" "db" {
              identifier                  = "spock-plano-de-custo"
              engine                      = "postgres"
              engine_version              = "16"
              instance_class              = "db.t4g.micro"
              allocated_storage           = 20
              storage_type                = "gp3"
              username                    = "app_admin"
              manage_master_user_password = true
              skip_final_snapshot         = true
            }

            resource "aws_ecs_task_definition" "app" {
              family                   = "spock-plano-de-custo"
              requires_compatibilities = ["FARGATE"]
              network_mode             = "awsvpc"
              cpu                      = "512"
              memory                   = "1024"
              container_definitions    = jsonencode([{ name = "app", image = "nginx", essential = true }])
            }

            resource "aws_ecs_service" "app" {
              name            = "spock-plano-de-custo"
              cluster         = "spock-plano-de-custo"
              task_definition = aws_ecs_task_definition.app.arn
              desired_count   = 2
              launch_type     = "FARGATE"

              network_configuration {
                subnets = ["subnet-0aaaaaaaaaaaaaaa1"]
              }
            }

            resource "aws_secretsmanager_secret" "app" {
              name = "spock-plano-de-custo"
            }
            """.Replace("\r\n", "\n"));
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var audit = new MemoryAudit();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var cloud = new Mariliz.Application.Cloud.CloudHub(new Mariliz.Infrastructure.Cloud.FileCloudStore(project.DataDir),
            new Mariliz.Application.Cloud.CloudProviderRegistry([new Mariliz.Infrastructure.Cloud.Aws.AwsCloudProvider()]), workspace, new SystemClock(), audit);
        var connection = await cloud.Connections.CreateAsync("aws", "Conta de teste", "profile", new Dictionary<string, string>
        {
            ["profile"] = Environment.GetEnvironmentVariable("MARILIZ_LIVE_AWS_PROFILE")!,
            ["region"] = "sa-east-1"
        }, CancellationToken.None);
        cloud.Projects.Link(projectId, connection.Id, ["aws.pricing"], agentAccess: false);
        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, cloud, new SystemClock(), audit);

        var plan = await operations.PlanAsync(new IacPlanRequest(projectId, "infra", null, connection.Id), IacOperationService.Person, CancellationToken.None);
        Assert.True(plan.Status == IacOperationStatus.Succeeded, plan.Error);
        var estimate = await new IacCostService(workspace, cloud, new SystemClock(), operations)
            .EstimatePlanAsync(new IacPlanCostRequest(projectId, plan.Id, connection.Id, null), Mariliz.Application.Cloud.CloudCaller.Person,
                CancellationToken.None);

        foreach (var line in estimate.Lines) output.WriteLine($"{line.Address} ({line.Detail}): {line.Before} -> {line.After} {line.Problem}");
        output.WriteLine($"Total: {estimate.MonthlyBefore} -> {estimate.MonthlyAfter} (+{estimate.MonthlyDelta}); fora: {string.Join(", ", estimate.Uncovered)}");
        Assert.Equal(4, estimate.Lines.Count);
        Assert.True(estimate.Complete, string.Join("; ", estimate.Lines.Select(l => l.Problem)));
        Assert.All(estimate.Lines, line => Assert.NotNull(line.After));
        Assert.Equal(61.90m, estimate.Lines.Single(l => l.Type == "aws_ecs_service").After);
        Assert.Contains("aws_ecs_task_definition", estimate.Uncovered);
    }

    // Entrega 9 com a conta real: uma subnet existente entra no state por import (só o state muda; a nuvem não), e o drift
    // pela conexão aponta as outras subnets da região como fora do state. Os ids vêm da leitura, nada fica no repositório.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS", "MARILIZ_LIVE_AWS_PROFILE")]
    public async Task Drift_aponta_subnets_da_conta_fora_do_state()
    {
        using var project = TempProject.Dotnet();
        var workspace = new WorkspaceAuthorizer(new LocalWorkspaceFileSystem(), new FileWorkspaceRegistry(project.DataDir));
        var projectId = workspace.Authorize(project.Root).Project!.ProjectId;
        var audit = new MemoryAudit();
        var profile = Environment.GetEnvironmentVariable("MARILIZ_LIVE_AWS_PROFILE")!;
        var cloud = new Mariliz.Application.Cloud.CloudHub(new Mariliz.Infrastructure.Cloud.FileCloudStore(project.DataDir),
            new Mariliz.Application.Cloud.CloudProviderRegistry([new Mariliz.Infrastructure.Cloud.Aws.AwsCloudProvider()]), workspace, new SystemClock(), audit);
        var connection = await cloud.Connections.CreateAsync("aws", "Conta de teste", "profile",
            new Dictionary<string, string> { ["profile"] = profile, ["region"] = "sa-east-1" }, CancellationToken.None);
        cloud.Projects.Link(projectId, connection.Id, ["aws.ec2.subnets"], agentAccess: false);
        var subnets = await cloud.Reads.ReadAsync(new Mariliz.Application.Cloud.CloudReadRequest(connection.Id, "aws.ec2.subnets", null, 50, projectId),
            Mariliz.Application.Cloud.CloudCaller.Person, CancellationToken.None);
        Assert.True(subnets.Rows.Count >= 2, "A conta precisa de pelo menos duas subnets em sa-east-1.");
        var managed = subnets.Rows[0];
        project.Write("infra/providers.tf", "terraform {\n  required_providers {\n    aws = {\n      source  = \"hashicorp/aws\"\n" +
                                            $"      version = \"{IacBootstrapTemplates.AwsVersion}\"\n    }}\n  }}\n}}\n\nprovider \"aws\" {{}}\n");
        project.Write("infra/resource.tf", $"resource \"aws_subnet\" \"existente\" {{\n  vpc_id     = \"{managed[2]}\"\n  cidr_block = \"{managed[4]}\"\n}}\n");

        using var tofu = new OpenTofuToolchain(project.DataDir, new NoExternal());
        var runner = new SystemProcessRunner();
        var runtimes = new IacRuntimeService([tofu], new FileIacSettingsStore(project.DataDir), new IacConfigReader(), runner, workspace, new FakeClock(), audit);
        var offer = await runtimes.OfferAsync(null, null, null, CancellationToken.None);
        var status = await runtimes.InstallAsync(offer.OfferId, offer.Version, IacEngine.OpenTofu, CancellationToken.None);
        var env = new Dictionary<string, string> { ["AWS_PROFILE"] = profile, ["AWS_REGION"] = "sa-east-1", ["TF_DATA_DIR"] = Path.Combine(project.DataDir, "import") };
        var infra = Path.Combine(project.Root, "infra");
        var init = await runner.RunAsync(new ProcessSpec(status.Selected!.Path, ["init", "-input=false", "-no-color"], infra, TimeSpan.FromMinutes(10), Environment: env),
            CancellationToken.None);
        var import = await runner.RunAsync(new ProcessSpec(status.Selected.Path, ["import", "-input=false", "-no-color", "aws_subnet.existente", managed[0]!], infra,
            TimeSpan.FromMinutes(5), Environment: env), CancellationToken.None);
        Assert.True(import.ExitCode == 0, init.StandardError + import.StandardError);

        var operations = new IacOperationService(runtimes, new CliIacRuntimeFactory(runner, project.DataDir), new IacConfigReader(),
            new FileIacWorkArea(project.DataDir), workspace, cloud, new SystemClock(), audit);
        var drift = await operations.DriftAsync(new IacPlanRequest(projectId, "infra", null, connection.Id), IacOperationService.Person, CancellationToken.None);

        output.WriteLine($"drift: {drift.Status} {drift.Error}; fora do state: {string.Join(", ", drift.Unmanaged?.Select(u => u.Id) ?? [])}; notas: " +
                         string.Join(" | ", drift.UnmanagedNotes ?? []));
        Assert.Equal(IacOperationStatus.Succeeded, drift.Status);
        Assert.DoesNotContain(drift.Unmanaged!, u => u.Id == managed[0]);
        Assert.Equal(subnets.Rows.Skip(1).Select(r => r[0]).Order(StringComparer.Ordinal), drift.Unmanaged!.Select(u => u.Id).Order(StringComparer.Ordinal));
    }

    // Entrega 6: drift de verdade. Aplica um local_file (provider hashicorp/local), apaga o arquivo por fora da ferramenta e a
    // verificação aponta o recurso como apagado, sem mudar o state do projeto.
    [LiveFact(Variable, "MARILIZ_LIVE_IAC_PROVIDERS")]
    public async Task OpenTofu_aponta_o_drift_de_um_recurso_apagado_por_fora()
    {
        using var project = TempProject.Dotnet();
        var outside = Directory.CreateTempSubdirectory("spock-drift-").FullName;
        var file = Path.Combine(outside, "nota.txt").Replace('\\', '/');
        project.Write("infra/providers.tf", "terraform {\n  required_version = \">= 1.6\"\n\n  required_providers {\n    local = {\n      source  = \"hashicorp/local\"\n      version = \"~> 2.5\"\n    }\n  }\n}\n");
        project.Write("infra/resource.tf", $"resource \"local_file\" \"nota\" {{\n  filename = \"{file}\"\n  content  = \"criado pelo plano\"\n}}\n");
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
        var request = new IacPlanRequest(projectId, "infra", null, null);

        var plan = await operations.PlanAsync(request, IacOperationService.Person, CancellationToken.None);
        var applied = await operations.ApplyAsync(new IacApplyRequest(projectId, plan.Id, plan.PlanId!, false, false, null), CancellationToken.None);
        var clean = await operations.DriftAsync(request, IacOperationService.Person, CancellationToken.None);
        File.Delete(file);
        var stateBefore = File.ReadAllText(Path.Combine(project.Root, "infra", "terraform.tfstate"));
        var drifted = await operations.DriftAsync(request, IacOperationService.Person, CancellationToken.None);

        output.WriteLine($"apply: {applied.Status}; limpo: {clean.Status} {clean.Error}; drift: {drifted.Status} {drifted.Error} " +
                         string.Join(", ", drifted.Drift?.Select(d => $"{d.Change} {d.Address}") ?? []));
        Assert.True(applied.Apply!.Verified);
        Assert.Equal(IacOperationStatus.Succeeded, clean.Status);
        Assert.False(clean.HasChanges);
        Assert.Empty(clean.Drift!);
        Assert.Equal(IacOperationStatus.Succeeded, drifted.Status);
        var item = Assert.Single(drifted.Drift!);
        Assert.Equal("local_file.nota", item.Address);
        Assert.Equal("deleted", item.Change);
        Assert.Equal(stateBefore, File.ReadAllText(Path.Combine(project.Root, "infra", "terraform.tfstate")));
        Assert.False(File.Exists(file));
        Directory.Delete(outside, recursive: true);
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
