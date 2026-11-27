namespace Mariliz.Application.Boot;

// A ordem é a do risco: o que vem depois exige o mesmo controle que o anterior, e mais.
public enum OperationClass
{
    Read,
    Query,
    Analysis,
    Preparation,
    Configuration,
    InternalChange,
    ExternalChange,
    Sensitive,
    Destructive
}

public enum AutonomyLevel
{
    Observation = 0,
    Preparation = 1,
    Assisted = 2,
    Governed = 3,
    Extended = 4
}

public enum CapabilityAvailability
{
    Available,
    Partial,
    Unavailable,
    NotConfigured,
    BlockedByPermission,
    DependsOnIntegration,
    Maintenance
}

// Direct: o Boot executa agora, porque é leitura ou preparação que só grava na pasta de dados do Spock.
// Handoff: o Boot prepara e abre a tela que já existe; quem pede a aprovação é ela.
// Composite: junta capacidades existentes em ordem, sem lógica própria.
public enum ExecutionMode
{
    Direct,
    Handoff,
    Composite
}

public enum BootMode
{
    Request,
    Project,
    Work,
    Recovery,
    Autonomous
}

public static class BootDomains
{
    public const string Engineering = "engineering";
    public const string Standards = "standards";
    public const string Management = "management";
    public const string Infrastructure = "infrastructure";
    public const string Ideas = "ideas";
    public const string Ai = "ai";
    public const string Connections = "connections";
    public const string Platform = "platform";

    public static readonly IReadOnlyList<string> All =
        [Engineering, Standards, Management, Infrastructure, Ideas, Ai, Connections, Platform];
}

[Flags]
public enum CapabilityNeeds
{
    None = 0,
    Project = 1,
    Model = 2,
    // Ao menos uma integração (GitHub, Jira) vinculada ao projeto.
    ProjectIntegration = 4,
    // Ao menos uma integração vinculada a algum projeto (portfólio).
    AnyIntegration = 8
}

public static class BootStates
{
    public const string Created = "created";
    public const string Interpreting = "interpreting";
    public const string AwaitingClarification = "awaitingClarification";
    public const string Preparing = "preparing";
    public const string Planned = "planned";
    public const string Executing = "executing";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Interrupted = "interrupted";

    public static bool IsTerminal(string state) => state is Completed or Failed or Cancelled or Interrupted or AwaitingClarification;

    // Um Boot que o motor deixou nestes estados não vai terminar sozinho.
    public static bool IsLive(string state) => state is Created or Interpreting or Preparing or Planned or Executing;
}

public static class BootOutcomes
{
    public const string Answered = "answered";
    public const string Prepared = "prepared";
    public const string Handoff = "handoff";
    public const string NeedsClarification = "needsClarification";
    public const string NeedsInput = "needsInput";
    public const string Unsupported = "unsupported";
    public const string Failed = "failed";
}

public static class StepStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public static class StepKinds
{
    public const string Check = "check";
    public const string Context = "context";
    public const string Capability = "capability";
    public const string Handoff = "handoff";
}
