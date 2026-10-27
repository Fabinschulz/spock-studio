using System.Text.Json;
using System.Text.Json.Serialization;
using Mariliz.Core.Evals;

namespace Mariliz.Application.Execution;

public static class RunStates
{
    public const string Analyzing = "analyzing";
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

public static class RunKinds
{
    public const string Apply = "apply";
    public const string Revert = "revert";
    public const string Validate = "validate";
}

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

/// <summary>Uma operação em um arquivo, com o hash antes e depois, para poder desfazê-la sem adivinhar.</summary>
public sealed class JournalOp
{
    public string Path { get; set; } = "";
    /// <summary>create | update | delete</summary>
    public string Op { get; set; } = "";
    /// <summary>write | command</summary>
    public string Source { get; set; } = "write";
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
    public string Status { get; set; } = "pending";
    public string? Detail { get; set; }
}

/// <summary>Registro de uma execução. É gravado antes e depois de cada operação, então sobrevive a uma queda.</summary>
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
}

public sealed class EvalSummary
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Required { get; set; }
}

public static class JournalJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };
}

/// <summary>Armazena diários e backups fora do repositório do projeto.</summary>
public interface IRunStore
{
    void SaveJournal(RunJournal journal);
    RunJournal? LoadJournal(string projectId, string runId);
    IReadOnlyList<RunJournal> ListJournals(string projectId);
    void SaveBackup(string projectId, string runId, string relativePath, byte[] content);
    byte[]? LoadBackup(string projectId, string runId, string relativePath);
}
