namespace Mariliz.Core.Text;

// Prefixo e 12 dígitos hexadecimais minúsculos (p- projeto, run- execução, ex- execução no runtime de IA, int- interrupção, apr- aprovação).
public static class Ids
{
    public const int HexLength = 12;

    public static bool IsProjectId(string? value) => Is(value, "p-");

    public static bool IsInterruptId(string? value) => Is(value, "int-");

    public static bool IsApprovalId(string? value) => Is(value, "apr-");

    public static bool Is(string? value, string prefix) =>
        value is not null
        && value.Length == prefix.Length + HexLength
        && value.StartsWith(prefix, StringComparison.Ordinal)
        && value.AsSpan(prefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
}
