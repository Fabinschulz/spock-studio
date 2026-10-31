using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

/// <summary>Gravado antes e depois de cada operação, então sobrevive a uma queda.</summary>
public sealed class RunJournal
{
    public string RunId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string State { get; set; } = RunStates.Running;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? RevertedByRunId { get; set; }
    public string? RevertsRunId { get; set; }
    public string? Error { get; set; }
    public bool ChangesApplied { get; set; }
    public bool RolledBack { get; set; }
    public List<JournalOp> Ops { get; set; } = [];
    public List<JournalStep> Steps { get; set; } = [];
    public List<string> NotApplied { get; set; } = [];
    public List<string> Pending { get; set; } = [];
    public List<EvalSummary> Evals { get; set; } = [];

    [JsonIgnore]
    public bool CanBeReverted =>ChangesApplied && !RolledBack && RevertedByRunId is null;
}
