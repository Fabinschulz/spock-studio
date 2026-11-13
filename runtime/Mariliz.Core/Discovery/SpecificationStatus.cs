namespace Mariliz.Core.Discovery;

public sealed record SpecificationStatus(
    SpecificationState State,
    string? Version,
    string? LockedHash,
    string? ComputedHash,
    bool VersionTested,
    IReadOnlyList<string> Problems);
