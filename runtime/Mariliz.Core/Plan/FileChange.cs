namespace Mariliz.Core.Plan;

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
