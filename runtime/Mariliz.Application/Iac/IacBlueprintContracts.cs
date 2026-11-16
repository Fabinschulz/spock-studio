namespace Mariliz.Application.Iac;

// O desenho da infraestrutura em dados: a IA (ou a pessoa) preenche, as checagens determinísticas conferem e os modelos
// fixos geram o Terraform. Nenhum HCL sai do modelo de linguagem. AWS (ECS Fargate, RDS, SQS), Azure (Container Apps,
// PostgreSQL Flexible Server, Service Bus) e Google Cloud (Cloud Run, Cloud SQL, Pub/Sub), no formato do modelo de
// referência (TERRAFORM_PROJECT_LAYOUT.md). Account é a conta AWS, a subscription do Azure ou o projeto do Google; Profile
// é o perfil da AWS CLI ou o tenant do Azure.
public sealed record IacBlueprint(
    string Provider,
    string Project,
    string Region,
    string? Account,
    string? Profile,
    IacNetwork Network,
    IacServiceSpec? Service,
    IacDatabaseSpec? Database,
    IReadOnlyList<string> Queues,
    string? AlarmEmail,
    IacBackendSpec? Backend);

// A rede já existe: o Terraform não cria rede, recebe os ids (do conector cloud ou digitados). No Azure, o banco usa a
// primeira subnet privada (delegada ao PostgreSQL) e a zona DNS privada; no Google, VpcId é a rede com acesso privado a
// serviços, onde o Cloud SQL ganha IP privado.
public sealed record IacNetwork(string VpcId, IReadOnlyList<string> PrivateSubnets, IReadOnlyList<string> PublicSubnets, string? PrivateDnsZone = null);

// Um serviço em contêiner no ECS Fargate atrás de um load balancer público só HTTPS, como a Api do modelo.
public sealed record IacServiceSpec(
    string Name,
    int Port,
    string HealthPath,
    int Cpu,
    int Memory,
    int DesiredCount,
    string CertificateArn,
    bool PublicIp);

// Banco gerenciado: senha criada e guardada pelo próprio RDS no Secrets Manager, nunca no Terraform.
public sealed record IacDatabaseSpec(string Engine, string EngineVersion, string InstanceClass, int StorageGb, bool MultiAz);

// O bucket de state (do bootstrap): sem ele, a raiz nasce sem backend e o relatório de state recomenda criar.
// Bucket é o bucket S3 ou GCS, ou a storage account do Azure, que também pede o grupo de recursos e o container.
public sealed record IacBackendSpec(string Bucket, string Region, string? ResourceGroup = null, string? Container = null);

public sealed record IacBlueprintCheck(string Id, string Description, bool Passed, string? Detail);

// O que a IA explica junto do desenho: por que cada serviço, riscos, alternativas e premissas. Custo não tem número
// inventado: só a lista do que é cobrado.
public sealed record IacArchitectureNotes(
    IReadOnlyList<IacServiceReason> Services,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> Assumptions);

public sealed record IacServiceReason(string Name, string Reason);

public sealed record IacArchitectureDraft(
    IacBlueprint? Blueprint,
    IacArchitectureNotes? Notes,
    IReadOnlyList<IacBlueprintCheck> Checks,
    bool Valid,
    int Attempts,
    int InputTokens,
    int OutputTokens);

public sealed record IacCostItem(string Resource, string Billing);

public sealed record IacGenerationPlan(
    string PlanId,
    string ProjectId,
    string Root,
    IReadOnlyList<Execution.ReviewedChange> Changes,
    IReadOnlyList<IacBlueprintCheck> Checks,
    IReadOnlyList<IacCostItem> Costs,
    IReadOnlyList<string> Notes);
