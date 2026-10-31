using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary><c>Mode</c>: "file" = a aplicação é dona do arquivo inteiro; "merge" = só de chaves dele.</summary>
public sealed record DesiredFile(string Path, ComponentKind Kind, string ComponentId, string Origin, string Reason, string Content, string Mode);
