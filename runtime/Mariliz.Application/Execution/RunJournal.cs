using System.Text.Json.Serialization;

namespace Mariliz.Application.Execution;

/// <summary>Gravado antes e depois de cada operação, então sobrevive a uma queda.</summary>
public sealed class RunJournal
{
    public string RunId { get; set; } = "";
    public string ProjectId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string PlanId { get; set; } = "";

    /// <summary>Identificador da execução no runtime. Só o workflow preenche; o diário do harness não.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExecutionId { get; set; }

    /// <summary>Pausa do grafo que está esperando aprovação e a aprovação que o motor abriu para ela.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InterruptId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApprovalId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ApprovedAt { get; set; }

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

    /// <summary>Tokens gastos com o modelo, como o proxy do motor contou. Só aparece quando a execução usou modelo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JournalModelUsage? ModelUsage { get; set; }

    /// <summary>A opinião do juiz por modelo, depois de conferida e ligada à chamada que o proxy viu. Não decide o resultado.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JournalJudgment? Judgment { get; set; }

    /// <summary>As ações propostas por agentes e o hash que liga a aprovação a elas. Vazio quando nenhum agente propôs nada.</summary>
    public List<JournalAction> Actions { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActionsHash { get; set; }

    [JsonIgnore]
    public bool CanBeReverted =>ChangesApplied && !RolledBack && RevertedByRunId is null;
}
