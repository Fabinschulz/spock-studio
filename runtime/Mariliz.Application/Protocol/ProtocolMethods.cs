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
    public const string ModelClear = "model.clear";
    public const string ModelComplete = "model.complete";

    public const string IntegrationCatalog = "integration.catalog";
    public const string ConnectionList = "connection.list";
    public const string ConnectionCreate = "connection.create";
    public const string ConnectionTest = "connection.test";
    public const string ConnectionRotate = "connection.rotate";
    public const string ConnectionDisconnect = "connection.disconnect";
    public const string ConnectionRemove = "connection.remove";
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
    public const string DecisionRecord = "decision.record";
    public const string DecisionList = "decision.list";
    public const string DecisionGet = "decision.get";
    public const string EngineeringProfile = "engineering.profile";
    public const string EngineeringAnalyze = "engineering.analyze";
    public const string EngineeringKnowledge = "engineering.knowledge";
    public const string EngineeringHarness = "engineering.harness";
    public const string EngineeringRevisions = "engineering.revisions";
    public const string EngineeringMcp = "engineering.mcp";
    public const string EngineeringCatalog = "engineering.catalog";

    public static readonly IReadOnlyList<string> Configure =
        [Discover, Plan, Apply, Validate, Revert, Runs];

    /// <summary>Consulta e cancelamento valem para execuções de qualquer jornada.</summary>
    public static readonly IReadOnlyList<string> RunControl = [Snapshot, Cancel, RunList, RunDetail, RunLink, RunUnlink, RunLinked];

    public static readonly IReadOnlyList<string> Workflow = [WorkflowStart, WorkflowResume];

    public static readonly IReadOnlyList<string> Model = [ModelStatus, ModelSave, ModelClear, ModelComplete];

    /// <summary>Central de integrações: conexões (<c>connection.*</c>) e o vínculo delas com um projeto (<c>integration.*</c>).</summary>
    public static readonly IReadOnlyList<string> Integrations =
    [
        IntegrationCatalog, ConnectionList, ConnectionCreate, ConnectionTest, ConnectionRotate, ConnectionDisconnect, ConnectionRemove,
        IntegrationList, IntegrationLink, IntegrationConfigure, IntegrationUnlink, IntegrationSync, IntegrationHistory
    ];

    /// <summary>Visão gerencial: só leitura do que já foi sincronizado.</summary>
    public static readonly IReadOnlyList<string> Management = [ManagementPortfolio, ManagementMetrics, ManagementForecast, ManagementRisks, ManagementRiskHistory, ManagementMilestones, ManagementReport, ManagementEvidence, ManagementAsk];

    /// <summary>Decisões gerenciais: registrar com os fatos do momento, listar e abrir. Sem editar nem apagar.</summary>
    public static readonly IReadOnlyList<string> Decision = [DecisionRecord, DecisionList, DecisionGet];

    /// <summary>Ambiente de Engenharia: perfil tecnológico, conhecimento, estado e revisões do Harness do projeto.</summary>
    public static readonly IReadOnlyList<string> Engineering = [EngineeringProfile, EngineeringAnalyze, EngineeringKnowledge, EngineeringHarness, EngineeringRevisions, EngineeringCatalog, EngineeringMcp];
}
