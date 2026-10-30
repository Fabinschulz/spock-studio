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

    public static readonly IReadOnlyList<string> Configure =
        [Discover, Plan, Apply, Validate, Revert, Runs, Snapshot, Cancel];
}
