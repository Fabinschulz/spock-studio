namespace Mariliz.Application.Protocol;

public static class ProtocolMethods
{
    public const string Hello = "engine.hello";
    public const string Shutdown = "engine.shutdown";
    public const string AuthorizeWorkspace = "workspace.authorize";

    public const string Discover = "project.discover";
    public const string Plan = "harness.plan";
    public const string Apply = "harness.apply";
    public const string Validate = "harness.validate";
    public const string Revert = "harness.revert";
    public const string Runs = "harness.runs";
    public const string Snapshot = "run.snapshot";
    public const string Cancel = "run.cancel";
    public const string RunList = "run.list";
    public const string RunDetail = "run.detail";
    public const string RunLink = "run.link";
    public const string RunUnlink = "run.unlink";
    public const string RunLinked = "run.linked";

    public const string WorkflowStart = "workflow.start";
    public const string WorkflowResume = "workflow.resume";
    public const string RuntimeHealth = "runtime.health";

    public const string ModelStatus = "model.status";
    public const string ModelSave = "model.save";
    public const string ModelRemove = "model.remove";
    public const string ModelDefault = "model.default";
    public const string ModelConnection = "model.connection";
    public const string ModelTest = "model.test";
    public const string ModelPurpose = "model.purpose";
    public const string ModelBudget = "model.budget";

    public const string IntegrationCatalog = "integration.catalog";
    public const string ConnectionList = "connection.list";
    public const string ConnectionCreate = "connection.create";
    public const string ConnectionTest = "connection.test";
    public const string ConnectionRotate = "connection.rotate";
    public const string ConnectionDisconnect = "connection.disconnect";
    public const string ConnectionRemove = "connection.remove";
    public const string ConnectionReach = "connection.reach";
    public const string IntegrationList = "integration.list";
    public const string IntegrationLink = "integration.link";
    public const string IntegrationConfigure = "integration.configure";
    public const string IntegrationUnlink = "integration.unlink";
    public const string IntegrationSync = "integration.sync";
    public const string IntegrationHistory = "integration.history";
    public const string ManagementPortfolio = "management.portfolio";
    public const string ManagementMetrics = "management.metrics";
    public const string ManagementForecast = "management.forecast";
    public const string ManagementRisks = "management.risks";
    public const string ManagementMilestones = "management.milestones";
    public const string ManagementRiskHistory = "management.riskhistory";
    public const string ManagementReport = "management.report";
    public const string ManagementEvidence = "management.evidence";
    public const string ManagementAsk = "management.ask";
    public const string ManagementInvestigate = "management.investigate";
    public const string ManagementPortfolioEvidence = "management.portfolioevidence";
    public const string ManagementPortfolioAsk = "management.portfolioask";
    public const string DecisionRecord = "decision.record";
    public const string DecisionList = "decision.list";
    public const string DecisionGet = "decision.get";
    public const string EngineeringProfile = "engineering.profile";
    public const string EngineeringAnalyze = "engineering.analyze";
    public const string EngineeringKnowledge = "engineering.knowledge";
    public const string EngineeringHarness = "engineering.harness";
    public const string EngineeringRevisions = "engineering.revisions";
    public const string EngineeringMcp = "engineering.mcp";
    public const string EngineeringPolicy = "engineering.policy";
    public const string EngineeringCatalog = "engineering.catalog";
    public const string TimelineRead = "timeline.read";
    public const string AuditRead = "audit.read";
    public const string AiToolList = "aitool.list";
    public const string AgentRun = "agent.run";
    public const string IdeConnection = "ide.connection";
    public const string ContextReview = "context.review";
    public const string SecurityScan = "security.scan";
    public const string SecurityPlan = "security.plan";
    public const string SecurityFix = "security.fix";
    public const string IdeaDraft = "idea.draft";
    public const string IdeaPlan = "idea.plan";
    public const string IdeaSave = "idea.save";
    public const string CloudCatalog = "cloud.catalog";
    public const string CloudList = "cloud.list";
    public const string CloudCreate = "cloud.create";
    public const string CloudValidate = "cloud.validate";
    public const string CloudPermissions = "cloud.permissions";
    public const string CloudRemove = "cloud.remove";
    public const string CloudLinks = "cloud.links";
    public const string CloudLink = "cloud.link";
    public const string CloudConfigure = "cloud.configure";
    public const string CloudUnlink = "cloud.unlink";
    public const string CloudRead = "cloud.read";
    public const string IacRuntimeStatus = "iac.runtime.status";
    public const string IacRuntimeOffer = "iac.runtime.offer";
    public const string IacRuntimeInstall = "iac.runtime.install";
    public const string IacRuntimeDecline = "iac.runtime.decline";
    public const string IacRuntimeRemove = "iac.runtime.remove";
    public const string IacRuntimeAdopt = "iac.runtime.adopt";
    public const string IacRuntimePin = "iac.runtime.pin";
    public const string IacLayout = "iac.layout";
    public const string IacValidate = "iac.validate";
    public const string IacPlan = "iac.plan";
    public const string IacOperations = "iac.operations";
    public const string IacOperation = "iac.operation";
    public const string IacApply = "iac.apply";

    public static readonly IReadOnlyList<string> Configure =
        [Discover, Plan, Apply, Validate, Revert, Runs];

    public static readonly IReadOnlyList<string> RunControl = [Snapshot, Cancel, RunList, RunDetail, RunLink, RunUnlink, RunLinked];

    public static readonly IReadOnlyList<string> Workflow = [WorkflowStart, WorkflowResume];

    public static readonly IReadOnlyList<string> Model = [ModelStatus, ModelSave, ModelRemove, ModelDefault, ModelConnection, ModelTest, ModelPurpose, ModelBudget];

    public static readonly IReadOnlyList<string> Integrations =
    [
        IntegrationCatalog, ConnectionList, ConnectionCreate, ConnectionTest, ConnectionRotate, ConnectionDisconnect, ConnectionRemove, ConnectionReach,
        IntegrationList, IntegrationLink, IntegrationConfigure, IntegrationUnlink, IntegrationSync, IntegrationHistory
    ];

    public static readonly IReadOnlyList<string> Management = [ManagementPortfolio, ManagementMetrics, ManagementForecast, ManagementRisks, ManagementRiskHistory, ManagementMilestones, ManagementReport, ManagementEvidence, ManagementAsk, ManagementInvestigate, ManagementPortfolioEvidence, ManagementPortfolioAsk];

    // O laço da sessão despacha estes pedidos sem esperar por eles: enquanto um roda, o motor segue respondendo e consegue cancelar, aprovar ou encerrar.
    public static readonly IReadOnlySet<string> Slow = new HashSet<string>(StringComparer.Ordinal)
    {
        ModelSave, ModelTest,
        ConnectionCreate, ConnectionTest, ConnectionRotate, ConnectionReach, IntegrationLink,
        ManagementAsk, ManagementInvestigate, ManagementPortfolioAsk,
        AiToolList, ContextReview,
        // Os três leem o projeto inteiro antes de responder.
        SecurityScan, SecurityPlan, SecurityFix,
        IdeaDraft,
        // Os quatro falam com o provedor de cloud.
        CloudCreate, CloudValidate, CloudPermissions, CloudRead,
        // Rodam o terraform da máquina, refazem o hash dos binários ou baixam da HashiCorp.
        IacRuntimeStatus, IacRuntimeOffer, IacRuntimeInstall, IacRuntimeDecline, IacRuntimeRemove, IacRuntimeAdopt, IacRuntimePin,
        // Rodam a ferramenta de IaC: init baixa providers e o plano lê o backend.
        IacValidate, IacPlan, IacApply
    };

    public static readonly IReadOnlyList<string> Decision = [DecisionRecord, DecisionList, DecisionGet];

    public static readonly IReadOnlyList<string> Timeline = [TimelineRead];

    public static readonly IReadOnlyList<string> Audit = [AuditRead];

    public static readonly IReadOnlyList<string> AiTool = [AiToolList];

    public static readonly IReadOnlyList<string> Agent = [AgentRun];

    public static readonly IReadOnlyList<string> Ide = [IdeConnection];

    public static readonly IReadOnlyList<string> Context = [ContextReview];

    public static readonly IReadOnlyList<string> Security = [SecurityScan, SecurityPlan, SecurityFix];

    public static readonly IReadOnlyList<string> Idea = [IdeaDraft, IdeaPlan, IdeaSave];

    public static readonly IReadOnlyList<string> Cloud =
        [CloudCatalog, CloudList, CloudCreate, CloudValidate, CloudPermissions, CloudRemove, CloudLinks, CloudLink, CloudConfigure, CloudUnlink, CloudRead];

    public static readonly IReadOnlyList<string> Iac =
        [IacRuntimeStatus, IacRuntimeOffer, IacRuntimeInstall, IacRuntimeDecline, IacRuntimeRemove, IacRuntimeAdopt, IacRuntimePin,
            IacLayout, IacValidate, IacPlan, IacOperations, IacOperation, IacApply];

    public static readonly IReadOnlyList<string> Engineering = [EngineeringProfile, EngineeringAnalyze, EngineeringKnowledge, EngineeringHarness, EngineeringRevisions, EngineeringCatalog, EngineeringMcp, EngineeringPolicy];
}
