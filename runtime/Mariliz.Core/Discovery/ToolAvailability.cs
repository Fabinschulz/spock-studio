namespace Mariliz.Core.Discovery;

// Versão nula = programa ausente.
/// <param name="Spock">Versão do lançador spock no PATH (DD-6); sem ele, o Harness não grava arquivos de IDE que o chamam.</param>
public sealed record ToolAvailability(string? Git, string? Node, string? Python, string? PythonCommand, string? Spock = null);
