namespace Mariliz.Application.Execution;

public static class OpStatus
{
    public const string Pending = "pending";
    public const string Applied = "applied";
    public const string RolledBack = "rolled_back";
    public const string Reverted = "reverted";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Conflict = "conflict";
}

public static class OpKinds
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
}

public static class OpSources
{
    public const string Write = "write";
    public const string Command = "command";
}

public static class StepStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string CompletedWithConflicts = "completed_with_conflicts";
}
