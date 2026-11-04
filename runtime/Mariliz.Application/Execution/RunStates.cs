namespace Mariliz.Application.Execution;

public static class RunStates
{
    public const string AwaitingApproval = "awaiting_approval";
    public const string Running = "running";
    public const string Validating = "validating";
    public const string Completed = "completed";
    public const string CompletedWithLimitations = "completed_with_limitations";
    public const string Failed = "failed";
    public const string RequiresIntervention = "requires_intervention";
    public const string Cancelled = "cancelled";

    public static bool IsTerminal(string state) =>
        state is Completed or CompletedWithLimitations or Failed or RequiresIntervention or Cancelled;
}
