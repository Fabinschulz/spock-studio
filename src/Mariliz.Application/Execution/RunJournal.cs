using System.Text.Json.Serialization;
using Mariliz.Core.Evals;

namespace Mariliz.Application.Execution;

public sealed class JournalOp
{
    public string Path { get; set; } = "";
    public string Op { get; set; } = "";
    public string Source { get; set; } = OpSources.Write;
    public string? BeforeHash { get; set; }
    public string? AfterHash { get; set; }
    public bool BackedUp { get; set; }
    public string Status { get; set; } = OpStatus.Pending;
    public string? Note { get; set; }
}

public sealed class JournalStep
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = StepStatus.Pending;
    public string? Detail { get; set; }
}

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

public sealed class EvalSummary
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Required { get; set; }

    public static List<EvalSummary> From(ValidationReport report) =>
        report.Results.Select(r => new EvalSummary { Id = r.Id, Status = r.Status.ToString(), Required = r.Required }).ToList();
}
