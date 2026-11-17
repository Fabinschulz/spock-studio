using System.Security.Cryptography;

namespace Mariliz.Core.Text;

// F1: o id que vai no mariliz.yaml e acompanha o repositório (clone, worktree, outra máquina). spk- e um ULID: 48 bits de
// tempo e 80 aleatórios, em Base32 de Crockford. O p-<hash do caminho> continua sendo a chave dos dados de cada cópia.
public static class StableProjectId
{
    public const string Prefix = "spk-";
    public const int UlidLength = 26;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static bool IsValid(string? value) =>
        value is not null
        && value.Length == Prefix.Length + UlidLength
        && value.StartsWith(Prefix, StringComparison.Ordinal)
        // O primeiro caractere do ULID só vai até 7: 26 caracteres de 5 bits passam dos 128.
        && value[Prefix.Length] <= '7'
        && value.AsSpan(Prefix.Length).IndexOfAnyExcept(Alphabet) < 0;

    public static string New(DateTimeOffset now)
    {
        Span<byte> bytes = stackalloc byte[16];
        var milliseconds = (ulong)now.ToUnixTimeMilliseconds();
        for (var i = 0; i < 6; i++) bytes[i] = (byte)(milliseconds >> (8 * (5 - i)));
        RandomNumberGenerator.Fill(bytes[6..]);

        var value = new UInt128(
            ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32) |
            ((ulong)bytes[4] << 24) | ((ulong)bytes[5] << 16) | ((ulong)bytes[6] << 8) | bytes[7],
            ((ulong)bytes[8] << 56) | ((ulong)bytes[9] << 48) | ((ulong)bytes[10] << 40) | ((ulong)bytes[11] << 32) |
            ((ulong)bytes[12] << 24) | ((ulong)bytes[13] << 16) | ((ulong)bytes[14] << 8) | bytes[15]);
        Span<char> chars = stackalloc char[UlidLength];
        for (var i = UlidLength - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }
        return Prefix + new string(chars);
    }
}
