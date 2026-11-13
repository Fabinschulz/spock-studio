using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Execution;

internal static class ProcessOutput
{
    private const int MaxTailLines = 15;
    private const int MaxTailLength = 1200;

    // Credenciais são omitidas antes de qualquer registro.
    public static string Tail(ProcessResult result)
    {
        var output = string.Join("\n", new[] { result.StandardError, result.StandardOutput }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var lines = SecretScanner.RedactLines(output)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(MaxTailLines)
            .ToList();

        if (lines.Count == 0) return " O programa não escreveu nenhuma saída.";

        var tail = string.Join(" | ", lines);
        return " Saída (final): " + (tail.Length > MaxTailLength ? tail[^MaxTailLength..] : tail);
    }
}
