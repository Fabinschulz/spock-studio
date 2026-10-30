namespace Mariliz.Core.Discovery;

/// <summary>Estado da estrutura de especificação no projeto: arquivos, versão e conferência do hash registrado pelo instalador.</summary>
public sealed record SpecificationStatus(
    SpecificationState State,
    string? Version,
    string? LockedHash,
    string? ComputedHash,
    bool VersionTested,
    IReadOnlyList<string> Problems);
