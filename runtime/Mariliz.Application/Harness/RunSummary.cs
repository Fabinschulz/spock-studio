namespace Mariliz.Application.Harness;

public sealed record RunSummary(string RunId, string Kind, string State, DateTimeOffset StartedAt, bool ChangesApplied, bool Reverted);
