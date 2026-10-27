namespace Mariliz.Core.Plan;

public enum ComponentKind { Sdd, Agent, Skill, Hook, Rule, Tool, Eval, Config }

public enum ChangeAction { Create, Update, Preserve, Remove, Conflict }

/// <summary>Como o usuário resolve um conflito: manter o arquivo dele ou substituir (com backup).</summary>
public enum ConflictResolution { Keep, Overwrite }

/// <summary>Uma mudança em um arquivo, com o estado atual, o desejado e o porquê.</summary>
public sealed record FileChange(
    string Id,
    ComponentKind Kind,
    string ComponentId,
    string Path,
    ChangeAction Action,
    string Reason,
    string Origin,
    string? BeforeHash,
    string? AfterHash,
    bool Destructive,
    string? ConflictReason,
    string? Diff,
    bool DiffTruncated);

/// <summary>Efeitos que um comando pode ter; a UI mostra todos antes da aprovação.</summary>
public enum CommandEffect { Process, Network, WriteLocal }

public sealed record PlannedCommand(
    string Id,
    string Description,
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<CommandEffect> Effects,
    bool Destructive,
    IReadOnlyList<string> AffectedPaths);

public sealed record Limitation(string Code, string Message);

/// <summary>Uma afirmação do planejamento, rotulada pela força da evidência.</summary>
public sealed record Observation(string Kind, string Text, IReadOnlyList<string> Evidence);

public sealed record ComponentDecision(
    ComponentKind Kind,
    string ComponentId,
    string Decision,
    string Justification,
    string Origin,
    IReadOnlyList<string> Evidence);

public sealed record ConfigurationPlan(
    string PlanId,
    string ProjectId,
    string AiEnvironment,
    string Sdd,
    string ProposalOrigin,
    IReadOnlyList<ComponentDecision> Decisions,
    IReadOnlyList<FileChange> Changes,
    IReadOnlyList<PlannedCommand> Commands,
    IReadOnlyList<Limitation> Limitations,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Validations)
{
    public bool IsEmpty =>
        Commands.Count == 0 && Changes.All(c => c.Action == ChangeAction.Preserve);
}
