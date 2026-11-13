namespace Mariliz.Core.Plan;

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
