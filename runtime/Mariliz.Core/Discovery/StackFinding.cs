namespace Mariliz.Core.Discovery;

/// <summary>Tecnologia detectada por arquivos de configuração, nunca pelo nome de uma pasta.</summary>
public sealed record StackFinding(
    string Id,
    string Name,
    string Root,
    string? Version,
    Confidence Confidence,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Notes);
