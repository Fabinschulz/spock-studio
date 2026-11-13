namespace Mariliz.Core.Discovery;

// Detectada por arquivos de configuração, nunca pelo nome de uma pasta.
public sealed record StackFinding(
    string Id,
    string Name,
    string Root,
    string? Version,
    Confidence Confidence,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<string> Notes);
