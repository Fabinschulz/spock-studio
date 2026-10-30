namespace Mariliz.Application.Harness;

public static class LimitationCodes
{
    public const string EnvironmentNotSelected = "ENVIRONMENT_NOT_SELECTED";
    public const string EnvironmentNotSupported = "ENVIRONMENT_NOT_SUPPORTED";
    public const string InvalidDeclaredConfig = "INVALID_DECLARED_CONFIG";
    public const string ProposalInvalid = "PROPOSAL_INVALID";
    public const string ProposalIgnored = "PROPOSAL_IGNORED";
    public const string NoRuleProfile = "NO_RULE_PROFILE";
    public const string HookBlocked = "HOOK_BLOCKED";
    public const string TlcBlocked = "TLC_BLOCKED";
    public const string TlcNeedsForce = "TLC_NEEDS_FORCE";
    public const string TlcUntested = "TLC_UNTESTED_VERSION";
    public const string TlcOutdated = "TLC_OUTDATED";
    public const string HarnessEngineerNotInvoked = "HARNESS_ENGINEER_NOT_INVOKED";
    public const string JudgeNotConfigured = "JUDGE_NOT_CONFIGURED";
}
