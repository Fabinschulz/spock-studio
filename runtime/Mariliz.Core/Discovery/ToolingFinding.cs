namespace Mariliz.Core.Discovery;

public sealed record ToolingFinding(
    string Kind,
    string Name,
    string? Command,
    IReadOnlyList<Evidence> Evidence);
