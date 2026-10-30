namespace Mariliz.Core.Discovery;

/// <summary>Programas do sistema de que o fluxo depende, com a versão encontrada (nulo = ausente).</summary>
public sealed record ToolAvailability(string? Git, string? Node, string? Python, string? PythonCommand);
