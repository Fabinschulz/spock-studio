namespace Mariliz.Application.Management;

public static class MilestoneOutlook
{
    public const string Done = "done";
    public const string Overdue = "overdue";
    public const string NoForecast = "no_forecast";
    public const string NoDueDate = "no_due_date";
    public const string OnTrack = "on_track";
    public const string AtRisk = "at_risk";
    public const string Behind = "behind";
}

public sealed record MilestoneProgress(
    string Kind,
    string Name,
    DateOnly? DueDate,
    int Items,
    int Delivered,
    int ClosedWithoutDelivery,
    int Open,
    string Outlook,
    string OutlookReason,
    DeliveryForecast Forecast);

public sealed record UnassignedItems(string Kind, int Items, int Open);

public sealed record ProjectMilestones(
    string ProjectId,
    DateTimeOffset GeneratedAt,
    bool Complete,
    IReadOnlyList<MilestoneProgress> Milestones,
    int Omitted,
    IReadOnlyList<UnassignedItems> Unassigned,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Limitations);
