namespace Mariliz.Core.Discovery;

public sealed record AiEnvironmentFinding(
    string Id,
    string Name,
    bool Supported,
    Confidence Confidence,
    IReadOnlyList<Evidence> Evidence);
