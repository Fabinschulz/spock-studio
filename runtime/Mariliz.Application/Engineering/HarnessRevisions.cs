namespace Mariliz.Application.Engineering;

public sealed record HarnessRevisionComponent(string Id, string Label, IReadOnlyList<string> Created, IReadOnlyList<string> Updated, IReadOnlyList<string> Removed);

/// <param name="RolledBack">A aplicação falhou no meio e o que tinha gravado foi desfeito na hora.</param>
public sealed record HarnessRevision(
    int Number,
    string RunId,
    string Kind,
    DateTimeOffset At,
    string State,
    bool RolledBack,
    string? RevertedByRunId,
    string? RevertsRunId,
    IReadOnlyList<HarnessRevisionComponent> Components);

public sealed record HarnessRevisions(
    string ProjectId,
    string? AppliedByEngine,
    string CurrentEngine,
    IReadOnlyList<HarnessRevision> Revisions);
