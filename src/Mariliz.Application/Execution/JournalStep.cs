using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

public sealed class JournalStep
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = StepStatus.Pending;
    public string? Detail { get; set; }
}
