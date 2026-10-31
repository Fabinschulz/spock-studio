namespace Mariliz.Application.Harness;

public sealed record ProposalRule(string Id, string Title, string Content, string Justification, IReadOnlyList<string> Evidence);
