namespace Mariliz.Application.Management.Assistant;

public sealed record AssistantStatement(string Text, IReadOnlyList<string> Evidence);

public sealed record RejectedStatement(string Section, string Text, string Reason);

public sealed record AssistantAnswer(
    string ProjectId,
    DateTimeOffset AnsweredAt,
    string Summary,
    IReadOnlyList<AssistantStatement> Observed,
    IReadOnlyList<AssistantStatement> Calculations,
    IReadOnlyList<AssistantStatement> Inferences,
    IReadOnlyList<AssistantStatement> Recommendations,
    IReadOnlyList<string> Unavailable,
    IReadOnlyList<RejectedStatement> Rejected,
    IReadOnlyList<EvidenceFact> Evidence,
    string Model,
    int InputTokens,
    int OutputTokens);
