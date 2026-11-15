using System.Security.Cryptography;
using System.Text;

namespace Mariliz.Infrastructure.Terraform;

// Verificação de assinatura OpenPGP destacada (RFC 4880) só no que a HashiCorp usa: chave RSA versão 4, assinatura de
// documento binário com SHA-256 ou SHA-512. O chaveiro é fixo no código, então a confiança vem dele e não das assinaturas
// de vínculo das subchaves, que não são conferidas aqui.
public sealed class OpenPgpKeyring
{
    private readonly Dictionary<string, RSAParameters> _keys;

    private OpenPgpKeyring(string primaryFingerprint, Dictionary<string, RSAParameters> keys)
    {
        PrimaryFingerprint = primaryFingerprint;
        _keys = keys;
    }

    public string PrimaryFingerprint { get; }

    public IReadOnlyCollection<string> Fingerprints => _keys.Keys;

    public static OpenPgpKeyring FromArmored(string armored)
    {
        var keys = new Dictionary<string, RSAParameters>(StringComparer.Ordinal);
        string? primary = null;
        foreach (var (tag, body) in OpenPgpPackets.Read(OpenPgpPackets.Dearmor(armored)))
        {
            // 6: chave pública; 14: subchave pública.
            if (tag is not (6 or 14)) continue;
            if (body.Length < 6 || body[0] != 4 || body[5] != 1) continue;
            var offset = 6;
            var modulus = OpenPgpPackets.Mpi(body, ref offset);
            var exponent = OpenPgpPackets.Mpi(body, ref offset);
            var fingerprint = Fingerprint(body.AsSpan(0, offset));
            keys[fingerprint] = new RSAParameters { Modulus = modulus, Exponent = exponent };
            if (tag == 6) primary ??= fingerprint;
        }
        if (primary is null) throw new FormatException("O chaveiro não tem chave primária RSA.");
        return new OpenPgpKeyring(primary, keys);
    }

    // Devolve a impressão digital da chave que assinou, ou null quando a assinatura não vale para este chaveiro.
    public string? Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        byte[] body;
        try
        {
            var packet = OpenPgpPackets.Read(signature.ToArray()).FirstOrDefault();
            if (packet.Tag != 2 || packet.Body is null) return null;
            body = packet.Body;
        }
        catch (FormatException)
        {
            return null;
        }

        // Versão 4, tipo 0x00 (documento binário), RSA.
        if (body.Length < 6 || body[0] != 4 || body[1] != 0x00 || body[2] != 1) return null;
        var hash = body[3] switch
        {
            8 => HashAlgorithmName.SHA256,
            10 => HashAlgorithmName.SHA512,
            _ => (HashAlgorithmName?)null
        };
        if (hash is null) return null;

        var hashedLength = (body[4] << 8) | body[5];
        if (6 + hashedLength + 2 > body.Length) return null;
        var hashedEnd = 6 + hashedLength;
        var issuer = IssuerFingerprint(body.AsSpan(6, hashedLength));
        if (issuer is null || !_keys.TryGetValue(issuer, out var key)) return null;

        var unhashedLength = (body[hashedEnd] << 8) | body[hashedEnd + 1];
        var offset = hashedEnd + 2 + unhashedLength + 2;
        if (offset > body.Length) return null;
        byte[] value;
        try
        {
            value = OpenPgpPackets.Mpi(body, ref offset);
        }
        catch (FormatException)
        {
            return null;
        }

        // O que é assinado: o documento, o começo do pacote até o fim dos subpacotes com hash e o trailer 0x04 0xFF + tamanho.
        var trailer = new byte[6];
        trailer[0] = 4;
        trailer[1] = 0xFF;
        trailer[2] = (byte)(hashedEnd >> 24);
        trailer[3] = (byte)(hashedEnd >> 16);
        trailer[4] = (byte)(hashedEnd >> 8);
        trailer[5] = (byte)hashedEnd;
        using var hasher = IncrementalHash.CreateHash(hash.Value);
        hasher.AppendData(data);
        hasher.AppendData(body, 0, hashedEnd);
        hasher.AppendData(trailer);
        var digest = hasher.GetHashAndReset();

        using var rsa = RSA.Create();
        rsa.ImportParameters(key);
        var size = key.Modulus!.Length;
        if (value.Length > size) return null;
        var padded = new byte[size];
        value.CopyTo(padded, size - value.Length);
        try
        {
            return rsa.VerifyHash(digest, padded, hash.Value, RSASignaturePadding.Pkcs1) ? issuer : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // Subpacote 33 (impressão digital do emissor, versão 4). Sem ele não há como escolher a chave com segurança.
    private static string? IssuerFingerprint(ReadOnlySpan<byte> subpackets)
    {
        var i = 0;
        while (i < subpackets.Length)
        {
            int length = subpackets[i++];
            if (length >= 192 && length < 255)
            {
                if (i >= subpackets.Length) return null;
                length = ((length - 192) << 8) + subpackets[i++] + 192;
            }
            else if (length == 255)
            {
                if (i + 4 > subpackets.Length) return null;
                length = (subpackets[i] << 24) | (subpackets[i + 1] << 16) | (subpackets[i + 2] << 8) | subpackets[i + 3];
                i += 4;
            }
            if (length < 1 || i + length > subpackets.Length) return null;
            var type = subpackets[i] & 0x7F;
            if (type == 33 && length == 22 && subpackets[i + 1] == 4)
                return Convert.ToHexString(subpackets.Slice(i + 2, 20));
            i += length;
        }
        return null;
    }

    private static string Fingerprint(ReadOnlySpan<byte> keyBody)
    {
        var prefix = new byte[] { 0x99, (byte)(keyBody.Length >> 8), (byte)keyBody.Length };
#pragma warning disable CA5350 // A impressão digital da versão 4 do OpenPGP é SHA-1 por definição; não protege nada sozinha.
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
#pragma warning restore CA5350
        sha1.AppendData(prefix);
        sha1.AppendData(keyBody);
        return Convert.ToHexString(sha1.GetHashAndReset());
    }
}

internal static class OpenPgpPackets
{
    public static byte[] Dearmor(string armored)
    {
        var body = new StringBuilder();
        var inBody = false;
        foreach (var raw in armored.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (!inBody)
            {
                if (line.Length == 0) inBody = true;
                continue;
            }
            if (line.StartsWith('=') || line.StartsWith("-----", StringComparison.Ordinal)) break;
            body.Append(line.Trim());
        }
        return Convert.FromBase64String(body.ToString());
    }

    public static IEnumerable<(int Tag, byte[] Body)> Read(byte[] data)
    {
        var i = 0;
        while (i < data.Length)
        {
            int header = data[i++];
            if ((header & 0x80) == 0) throw new FormatException("Pacote OpenPGP inválido.");
            int tag;
            long length;
            if ((header & 0x40) != 0)
            {
                tag = header & 0x3F;
                if (i >= data.Length) throw new FormatException("Pacote OpenPGP truncado.");
                int first = data[i++];
                if (first < 192) length = first;
                else if (first < 224)
                {
                    if (i >= data.Length) throw new FormatException("Pacote OpenPGP truncado.");
                    length = ((first - 192) << 8) + data[i++] + 192;
                }
                else if (first == 255)
                {
                    if (i + 4 > data.Length) throw new FormatException("Pacote OpenPGP truncado.");
                    length = ((long)data[i] << 24) | ((long)data[i + 1] << 16) | ((long)data[i + 2] << 8) | data[i + 3];
                    i += 4;
                }
                else throw new FormatException("Tamanho parcial de pacote OpenPGP não é aceito.");
            }
            else
            {
                tag = (header >> 2) & 0x0F;
                var lengthType = header & 0x03;
                if (lengthType == 3) throw new FormatException("Tamanho indeterminado de pacote OpenPGP não é aceito.");
                var bytes = 1 << lengthType;
                if (i + bytes > data.Length) throw new FormatException("Pacote OpenPGP truncado.");
                length = 0;
                for (var k = 0; k < bytes; k++) length = (length << 8) | data[i + k];
                i += bytes;
            }
            if (length < 0 || i + length > data.Length) throw new FormatException("Pacote OpenPGP truncado.");
            yield return (tag, data[i..(i + (int)length)]);
            i += (int)length;
        }
    }

    public static byte[] Mpi(byte[] data, ref int offset)
    {
        if (offset + 2 > data.Length) throw new FormatException("Número OpenPGP truncado.");
        var bits = (data[offset] << 8) | data[offset + 1];
        var length = (bits + 7) / 8;
        if (offset + 2 + length > data.Length) throw new FormatException("Número OpenPGP truncado.");
        var value = data.AsSpan(offset + 2, length).ToArray();
        offset += 2 + length;
        return value;
    }
}
