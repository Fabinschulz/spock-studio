namespace Mariliz.Core.Discovery;

// Versão nula = programa ausente.
public sealed record ToolAvailability(string? Git, string? Node, string? Python, string? PythonCommand);
