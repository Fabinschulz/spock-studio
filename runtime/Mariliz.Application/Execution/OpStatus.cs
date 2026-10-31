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
