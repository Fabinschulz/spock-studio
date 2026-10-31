namespace Mariliz.Core.Plan;

/// <summary>Uma afirmação do planejamento, rotulada pela força da evidência.</summary>
public sealed record Observation(string Kind, string Text, IReadOnlyList<string> Evidence);
