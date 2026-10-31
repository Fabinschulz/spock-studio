namespace Mariliz.Core.Discovery;

public static class DiscoveryWarningCodes
{
    public const string UncommittedChanges = "UNCOMMITTED_CHANGES";
    public const string NotAGitRepository = "NOT_A_GIT_REPOSITORY";
    public const string MultipleAiEnvironments = "MULTIPLE_AI_ENVIRONMENTS";
    public const string NoAiEnvironment = "NO_AI_ENVIRONMENT_DETECTED";
    public const string ConflictingPackageManagers = "CONFLICTING_PACKAGE_MANAGERS";
    public const string UnknownVersion = "UNKNOWN_VERSION";
    public const string SpecificationDrift = "SPECIFICATION_DRIFT";
    public const string SpecificationUntestedVersion = "SPECIFICATION_UNTESTED_VERSION";
    public const string UnrecognizedHarnessFiles = "UNRECOGNIZED_HARNESS_FILES";
    public const string NoStackDetected = "NO_STACK_DETECTED";
    public const string PythonMissing = "PYTHON_MISSING";
    public const string NodeMissing = "NODE_MISSING";
    public const string InvalidConfig = "INVALID_CONFIG";
}
