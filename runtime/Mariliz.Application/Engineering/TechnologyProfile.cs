using Mariliz.Core.Discovery;

namespace Mariliz.Application.Engineering;

public sealed record ProfileTechnology(
    string Id,
    string Name,
    string Category,
    IReadOnlyList<string> Versions,
    IReadOnlyList<string> Roots,
    Confidence Confidence,
    IReadOnlyList<string> Evidence);

public sealed record ProfileTool(string Kind, string Name, string? Command);

public sealed record ProfileConnection(string Role, string Provider, string Resource);

public sealed record ProfileHarnessGuidance(string Technology, string? RuleId, bool Covered, string Detail);

public sealed record ProfileChange(DateTimeOffset At, string Kind, string Subject, string? From, string? To);

public sealed record TechnologyProfile(
    string ProjectId,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<ProfileTechnology> Technologies,
    IReadOnlyList<ProfileTool> Tooling,
    IReadOnlyList<string> AiEnvironments,
    IReadOnlyList<ProfileConnection> Connections,
    IReadOnlyList<ProfileHarnessGuidance> Harness,
    IReadOnlyList<ProfileChange> History);
