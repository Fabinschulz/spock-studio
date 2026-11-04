namespace Mariliz.Core.Text;

/// <summary>
/// Forma dos identificadores que o motor gera: prefixo e 12 dígitos hexadecimais minúsculos (<c>p-</c> projeto,
/// <c>run-</c> execução, <c>ex-</c> execução no runtime de IA, <c>int-</c> interrupção, <c>apr-</c> aprovação).
/// </summary>
public static class Ids
{
    public const int HexLength = 12;

    public static bool IsProjectId(string? value) => Is(value, "p-");

    public static bool IsInterruptId(string? value) => Is(value, "int-");

    public static bool Is(string? value, string prefix) =>
        value is not null
        && value.Length == prefix.Length + HexLength
        && value.StartsWith(prefix, StringComparison.Ordinal)
        && value.AsSpan(prefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
}
