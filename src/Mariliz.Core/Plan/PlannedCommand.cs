namespace Mariliz.Core.Plan;

public sealed record PlannedCommand(
    string Id,
    string Description,
    string Executable,
    IReadOnlyList<string> Arguments,
    IReadOnlyList<CommandEffect> Effects,
    bool Destructive,
    IReadOnlyList<string> AffectedPaths);
