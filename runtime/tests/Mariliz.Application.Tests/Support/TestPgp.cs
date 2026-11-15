using System.Security.Cryptography;
using System.Text;
using Mariliz.Infrastructure.Iac;

namespace Mariliz.Application.Tests.Support;

// Uma chave OpenPGP RSA de teste e assinaturas destacadas no mesmo formato da HashiCorp, para provar a instalação sem rede.
internal sealed class TestPgp : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly byte[] _keyBody;

    public TestPgp()
    {
        var parameters = _rsa.ExportParameters(false);
        _keyBody = [4, 0x65, 0x00, 0x00, 0x00, 1, .. Mpi(parameters.Modulus!), .. Mpi(parameters.Exponent!)];
        Fingerprint = Convert.ToHexString(SHA1.HashData([0x99, (byte)(_keyBody.Length >> 8), (byte)_keyBody.Length, .. _keyBody]));
    }

    public string Fingerprint { get; }

    public OpenPgpKeyring Keyring() => OpenPgpKeyring.FromArmored(
        "-----BEGIN PGP PUBLIC KEY BLOCK-----\n\n" + Convert.ToBase64String(Packet(6, _keyBody)) + "\n=AAAA\n-----END PGP PUBLIC KEY BLOCK-----\n");

    public byte[] Sign(byte[] data)
    {
        byte[] hashed = [22, 33, 4, .. Convert.FromHexString(Fingerprint)];
        byte[] head = [4, 0x00, 1, 8, (byte)(hashed.Length >> 8), (byte)hashed.Length, .. hashed];
        byte[] trailer = [4, 0xFF, 0, 0, (byte)(head.Length >> 8), (byte)head.Length];
        var digest = SHA256.HashData([.. data, .. head, .. trailer]);
        var signature = _rsa.SignHash(digest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Packet(2, [.. head, 0, 0, digest[0], digest[1], .. Mpi(signature)]);
    }

    public void Dispose() => _rsa.Dispose();

    private static byte[] Mpi(byte[] value)
    {
        var start = 0;
        while (start < value.Length - 1 && value[start] == 0) start++;
        var trimmed = value[start..];
        var bits = (trimmed.Length - 1) * 8 + (8 - System.Numerics.BitOperations.LeadingZeroCount((uint)trimmed[0]) + 24);
        return [(byte)(bits >> 8), (byte)bits, .. trimmed];
    }

    private static byte[] Packet(int tag, byte[] body)
    {
        var length = body.Length;
        byte[] header = length < 192 ? [(byte)(0xC0 | tag), (byte)length]
            : [(byte)(0xC0 | tag), (byte)(((length - 192) >> 8) + 192), (byte)((length - 192) & 0xFF)];
        return [.. header, .. body];
    }
}

// Responde como releases.hashicorp.com e a API de versões, a partir de arquivos em memória; registra cada endereço pedido.
internal sealed class FakeHashiCorp : HttpMessageHandler
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Requested { get; } = [];

    public static string Url(string version, string file) => $"https://releases.hashicorp.com/terraform/{version}/{file}";

    public void Publish(string version, string platform, byte[] zip, TestPgp signer, Func<string, string>? tamperSums = null)
    {
        var archive = $"terraform_{version}_{platform}.zip";
        var sums = $"{Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant()}  {archive}\n";
        var sumsBytes = Encoding.UTF8.GetBytes(sums);
        Files[Url(version, archive)] = zip;
        Files[Url(version, $"terraform_{version}_SHA256SUMS")] = tamperSums is null ? sumsBytes : Encoding.UTF8.GetBytes(tamperSums(sums));
        Files[Url(version, $"terraform_{version}_SHA256SUMS.{signer.Fingerprint[^8..]}.sig")] = signer.Sign(sumsBytes);
        var (os, arch) = (platform.Split('_')[0], platform.Split('_')[1]);
        var release = $$"""
            {"version":"{{version}}","is_prerelease":false,"timestamp_created":"2026-11-11T11:57:56.765Z",
             "url_shasums":"{{Url(version, $"terraform_{version}_SHA256SUMS")}}",
             "url_shasums_signatures":["{{Url(version, $"terraform_{version}_SHA256SUMS.{signer.Fingerprint[^8..]}.sig")}}"],
             "url_changelog":"https://github.com/hashicorp/terraform/blob/v1/CHANGELOG.md",
             "builds":[{"os":"{{os}}","arch":"{{arch}}","url":"{{Url(version, archive)}}"}]}
            """;
        Files[$"https://api.releases.hashicorp.com/v1/releases/terraform/{version}"] = Encoding.UTF8.GetBytes(release);
        var list = Files.Where(f => f.Key.StartsWith("https://api.releases.hashicorp.com/v1/releases/terraform/", StringComparison.Ordinal))
            .Select(f => Encoding.UTF8.GetString(f.Value));
        Files["https://api.releases.hashicorp.com/v1/releases/terraform?limit=20"] = Encoding.UTF8.GetBytes("[" + string.Join(",", list) + "]");
    }

    public static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }
        return buffer.ToArray();
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requested.Add(url);
        return Task.FromResult(Files.TryGetValue(url, out var body)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}

// Responde como get.opentofu.org e as releases do GitHub do OpenTofu: o download na github.com redireciona para a CDN.
internal sealed class FakeOpenTofu : HttpMessageHandler
{
    public const string Cdn = "https://release-assets.githubusercontent.com/asset/";

    private readonly List<string> _versions = [];

    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Requested { get; } = [];

    // Para onde o github.com manda o download; trocar prova que só a CDN do GitHub é seguida.
    public string RedirectBase { get; set; } = Cdn;

    public static string Url(string version, string file) => $"https://github.com/opentofu/opentofu/releases/download/v{version}/{file}";

    public void Publish(string version, string platform, byte[] zip, TestPgp signer)
    {
        var archive = $"tofu_{version}_{platform}.zip";
        var sums = Encoding.UTF8.GetBytes($"{Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant()}  {archive}\n");
        Files[Cdn + archive] = zip;
        Files[Cdn + $"tofu_{version}_SHA256SUMS"] = sums;
        Files[Cdn + $"tofu_{version}_SHA256SUMS.gpgsig"] = signer.Sign(sums);
        _versions.Insert(0, $$"""{"id":"{{version}}","files":["{{archive}}","tofu_{{version}}_SHA256SUMS","tofu_{{version}}_SHA256SUMS.gpgsig"]}""");
        Files[OpenTofuApi] = Encoding.UTF8.GetBytes("{\"versions\":[" + string.Join(",", _versions) + "]}");
    }

    private const string OpenTofuApi = "https://get.opentofu.org/tofu/api.json";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requested.Add(url);
        const string github = "https://github.com/opentofu/opentofu/releases/download/";
        if (url.StartsWith(github, StringComparison.Ordinal))
        {
            var redirect = new HttpResponseMessage(System.Net.HttpStatusCode.Found);
            redirect.Headers.Location = new Uri(RedirectBase + url[(url.LastIndexOf('/') + 1)..]);
            return Task.FromResult(redirect);
        }
        return Task.FromResult(Files.TryGetValue(url, out var body)
            ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
            : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}
