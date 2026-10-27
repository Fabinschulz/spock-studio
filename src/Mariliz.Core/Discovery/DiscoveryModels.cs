namespace Mariliz.Core.Discovery;

public enum Confidence { High, Medium, Low }

/// <summary>Um fato observado: o arquivo (caminho relativo, com '/') e o valor lido dele.</summary>
public sealed record Evidence(string Path, string? Detail = null);

/// <summary>Tecnologia detectada por arquivos de configuração, nunca pelo nome de uma pasta.</summary>
public sealed record StackFinding(
    string Id,
    string Name,
    string Root,
    string? Version,
    Confidence Confidence,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Notes);

/// <summary>Ferramenta de build, gerenciador de pacotes, teste ou pipeline.</summary>
public sealed record ToolingFinding(
    string Kind,
    string Name,
    string? Command,
    IReadOnlyList<Evidence> Evidence);

public sealed record AiEnvironmentFinding(
    string Id,
    string Name,
    bool Supported,
    Confidence Confidence,
    IReadOnlyList<Evidence> Evidence);

public sealed record InventoryItem(string Name, string Path, bool Managed, string? Issue);

/// <summary>O que o projeto já tem de agentes, skills, hooks, regras, MCPs e evals.</summary>
public sealed record HarnessInventory(
    IReadOnlyList<InventoryItem> Agents,
    IReadOnlyList<InventoryItem> Skills,
    IReadOnlyList<InventoryItem> Hooks,
    IReadOnlyList<InventoryItem> Rules,
    IReadOnlyList<InventoryItem> McpServers,
    IReadOnlyList<InventoryItem> Evals)
{
    public static HarnessInventory Empty { get; } = new([], [], [], [], [], []);
}

public enum TlcState { NotInstalled, Installed, Drifted, Invalid }

/// <summary>Estado do TLC no projeto: arquivos, versão e conferência do hash registrado pelo instalador.</summary>
public sealed record TlcStatus(
    TlcState State,
    string? Version,
    string? LockedHash,
    string? ComputedHash,
    bool VersionTested,
    IReadOnlyList<string> Problems);

public sealed record GitState(bool IsRepository, bool? HasUncommittedChanges, IReadOnlyList<string> ChangedPaths);

public sealed record DiscoveryWarning(string Code, string Message, IReadOnlyList<string> Paths);

public sealed record DiscoveryReport(
    string ProjectId,
    string ProjectName,
    IReadOnlyList<StackFinding> Stacks,
    IReadOnlyList<ToolingFinding> Tooling,
    IReadOnlyList<AiEnvironmentFinding> AiEnvironments,
    HarnessInventory Inventory,
    TlcStatus Tlc,
    GitState Git,
    ToolAvailability Tools,
    bool HasDeclaredConfig,
    IReadOnlyList<DiscoveryWarning> Warnings);

/// <summary>Programas do sistema de que o fluxo depende, com a versão encontrada (nulo = ausente).</summary>
public sealed record ToolAvailability(string? Git, string? Node, string? Python, string? PythonCommand);

public static class DiscoveryWarningCodes
{
    public const string UncommittedChanges = "UNCOMMITTED_CHANGES";
    public const string NotAGitRepository = "NOT_A_GIT_REPOSITORY";
    public const string MultipleAiEnvironments = "MULTIPLE_AI_ENVIRONMENTS";
    public const string NoAiEnvironment = "NO_AI_ENVIRONMENT_DETECTED";
    public const string ConflictingPackageManagers = "CONFLICTING_PACKAGE_MANAGERS";
    public const string UnknownVersion = "UNKNOWN_VERSION";
    public const string TlcDrift = "TLC_DRIFT";
    public const string TlcUntestedVersion = "TLC_UNTESTED_VERSION";
    public const string UnrecognizedHarnessFiles = "UNRECOGNIZED_HARNESS_FILES";
    public const string NoStackDetected = "NO_STACK_DETECTED";
    public const string PythonMissing = "PYTHON_MISSING";
    public const string NodeMissing = "NODE_MISSING";
    public const string InvalidConfig = "INVALID_CONFIG";
}
