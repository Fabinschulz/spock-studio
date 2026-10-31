namespace Mariliz.Core.Discovery;

/// <summary>Ferramenta de build, gerenciador de pacotes, teste ou pipeline.</summary>
public sealed record ToolingFinding(
    string Kind,
    string Name,
    string? Command,
    IReadOnlyList<Evidence> Evidence);
