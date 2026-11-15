using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

// Gravado antes e depois de cada operação, então sobrevive a uma queda.
public sealed class RunJournal
{
    public string RunId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string PlanId { get; set; } = "";

    // Só o workflow preenche; o diário do harness não.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExecutionId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterruptId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApprovalId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ApprovedAt { get; set; }

    // Quantas ações a pessoa marcou na decisão. Zero é uma recusa, mesmo com ApprovedAt preenchido; nulo em diário de antes deste campo.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ApprovedActions { get; set; }

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JournalModelUsage? ModelUsage { get; set; }

    // Não decide o resultado.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JournalJudgment? Judgment { get; set; }

    public List<JournalAction> Actions { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActionsHash { get; set; }

    [JsonIgnore]
    public bool CanBeReverted =>ChangesApplied && !RolledBack && RevertedByRunId is null;
}
