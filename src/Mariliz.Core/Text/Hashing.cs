using System.Security.Cryptography;
using System.Text;

namespace Mariliz.Core.Text;

public static class Hashing
{
    /// <summary>SHA-256 em hexadecimal minúsculo dos bytes exatos.</summary>
    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string Sha256(string text) => Sha256(Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// Hash do conteúdo de uma skill do instalador do TLC: SHA-256 sobre cada caminho relativo
    /// (com '/', ordenado) seguido dos bytes do arquivo. Verificado contra o lockfile real do TLC.
    /// </summary>
    public static string SkillContentHash(IReadOnlyDictionary<string, byte[]> files)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in files.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            sha.AppendData(Encoding.UTF8.GetBytes(path));
            sha.AppendData(files[path]);
        }
        return Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
    }
}
