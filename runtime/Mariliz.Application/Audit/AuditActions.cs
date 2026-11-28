namespace Mariliz.Application.Audit;

// Os nomes gravados no arquivo de auditoria. Quem grava e a trilha do projeto que os descreve leem daqui, e o
// teste de cobertura exige descrição própria para toda ação de projeto.
public static class AuditActions
{
    public const string IntegrationLinked = "integration.linked";
    public const string IntegrationScopeChanged = "integration.scope_changed";
    public const string IntegrationUnlinked = "integration.unlinked";
    public const string IntegrationSynced = "integration.synced";
    public const string IntegrationSyncListenerFailed = "integration.sync_listener_failed";
    public const string IntegrationSyncBookkeepingFailed = "integration.sync_bookkeeping_failed";

    public const string ConnectionCreated = "connection.created";
    public const string ConnectionCredentialUpdated = "connection.credential_updated";
    public const string ConnectionDisconnected = "connection.disconnected";
    public const string ConnectionRemoved = "connection.removed";

    public const string CloudLinked = "cloud.linked";
    public const string CloudScopeChanged = "cloud.scope_changed";
    public const string CloudUnlinked = "cloud.unlinked";
    public const string CloudRead = "cloud.read";
    public const string CloudConnectionCreated = "cloud.connection_created";
    public const string CloudConnectionValidated = "cloud.connection_validated";
    public const string CloudPermissionsChecked = "cloud.permissions_checked";
    public const string CloudConnectionRemoved = "cloud.connection_removed";

    public const string DecisionRecorded = "decision.recorded";
    public const string IdeContextRead = "ide.context_read";
    public const string IdeBootstrap = "ide.bootstrap";
    public const string IdePrompt = "ide.prompt";
    public const string IdeBoot = "ide.boot";
    public const string RunLinked = "run.linked";
    public const string RunUnlinked = "run.unlinked";
    public const string ProfileRefreshed = "engineering.profile_refreshed";
    public const string AssistantAnswered = "management.assistant_answered";
    public const string InvestigationAnswered = "management.investigation_answered";
    public const string PortfolioAnswered = "management.portfolio_answered";
    public const string BootStarted = "boot.started";
    public const string BootFinished = "boot.finished";
    public const string BootIntentModel = "boot.intent_model";
    public const string QualityReviewed = "quality.reviewed";
    public const string QualityFindingDecided = "quality.finding_decided";
    public const string QualityDeliveryDecided = "quality.delivery_decided";

    public const string IacValidate = "iac.validate";
    public const string IacPlan = "iac.plan";
    public const string IacDrift = "iac.drift";
    public const string IacApply = "iac.apply";
    public const string IacApplyApproved = "iac.apply_approved";
    public const string IacDriftScheduleChanged = "iac.drift_schedule_changed";
    public const string IacDriftScheduleFailed = "iac.drift_schedule_failed";
    public const string IacBootstrapWritten = "iac.bootstrap_written";
    public const string IacGenerated = "iac.generated";
    public const string IacArchitectureDrafted = "iac.architecture_drafted";
    public const string IacStateRead = "iac.state_read";
    public const string IacStateUnlocked = "iac.state_unlocked";
    public const string IacRuntimePinned = "iac.runtime.pinned";
    public const string IacRuntimeUnpinned = "iac.runtime.unpinned";
    public const string IacRuntimeInstalled = "iac.runtime.installed";
    public const string IacRuntimeInstallFailed = "iac.runtime.install_failed";
    public const string IacRuntimeInstallBlocked = "iac.runtime.install_blocked";
    public const string IacRuntimeDeclined = "iac.runtime.declined";
    public const string IacRuntimeRemoved = "iac.runtime.removed";
    public const string IacRuntimeAdopted = "iac.runtime.adopted";
    public const string IacRuntimeAdoptBlocked = "iac.runtime.adopt_blocked";

    // Valem para o computador inteiro, não para um projeto: não carregam projectId e não entram na trilha de um projeto.
    public static readonly IReadOnlySet<string> MachineWide = new HashSet<string>(StringComparer.Ordinal)
    {
        PortfolioAnswered,
        IacRuntimeInstalled, IacRuntimeInstallFailed, IacRuntimeInstallBlocked, IacRuntimeDeclined,
        IacRuntimeRemoved, IacRuntimeAdopted, IacRuntimeAdoptBlocked
    };

    public static bool IsConnection(string action) =>
        action.StartsWith("connection.", StringComparison.Ordinal)
        || action.StartsWith("cloud.connection_", StringComparison.Ordinal)
        || action == CloudPermissionsChecked;
}
