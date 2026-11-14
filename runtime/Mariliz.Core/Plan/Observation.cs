namespace Mariliz.Core.Plan;

public sealed record Observation(string Kind, string Text, IReadOnlyList<string> Evidence);
