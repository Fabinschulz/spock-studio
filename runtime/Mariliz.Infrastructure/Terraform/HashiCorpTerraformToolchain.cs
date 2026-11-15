using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Terraform;

// Instalações em <dados>/tools/terraform/<versão>/, vindas só de releases.hashicorp.com: a lista de checksums tem de ter
// assinatura válida de uma chave do chaveiro embutido e o pacote tem de bater com ela. O binário nunca vai para o PATH.
public sealed class HashiCorpTerraformToolchain : ITerraformToolchain, IDisposable
{
    public const string ReleasesApi = "https://api.releases.hashicorp.com/v1/releases/terraform";
    public const string DownloadHost = "releases.hashicorp.com";
    private const string ManifestName = "install.json";
    private const long MaxArchiveBytes = 256L * 1024 * 1024;
    private const int MaxSmallFileBytes = 1024 * 1024;

    private static readonly Lazy<OpenPgpKeyring> EmbeddedKeyring = new(() =>
    {
        using var stream = typeof(HashiCorpTerraformToolchain).Assembly.GetManifestResourceStream("Mariliz.Infrastructure.Terraform.hashicorp-release-key.asc")
            ?? throw new InvalidOperationException("Chave da HashiCorp ausente do pacote.");
        using var reader = new StreamReader(stream);
        return OpenPgpKeyring.FromArmored(reader.ReadToEnd());
    });

    private readonly string _root;
    private readonly IExecutableLocator _locator;
    private readonly OpenPgpKeyring _keyring;
    private readonly HttpClient _http;

    public HashiCorpTerraformToolchain(string dataRoot, IExecutableLocator locator, HttpMessageHandler? handler = null,
        TerraformPlatform? platform = null, OpenPgpKeyring? keyring = null)
    {
        _root = Path.Combine(dataRoot, "tools", "terraform");
        _locator = locator;
        _keyring = keyring ?? EmbeddedKeyring.Value;
        Platform = platform ?? Detect();
        // Sem redirecionamento: o pacote só pode vir do endereço conferido, nunca de onde uma resposta mandar.
        _http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromMinutes(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SpockStudio");
    }

    public TerraformPlatform? Platform { get; }

    public string SigningKey => _keyring.PrimaryFingerprint;

    public IReadOnlyList<ManagedTerraform> Managed()
    {
        if (Platform is null || !Directory.Exists(_root)) return [];
        var found = new List<ManagedTerraform>();
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            var name = Path.GetFileName(directory);
            if (TerraformVersion.TryParse(name) is null) continue;
            var manifest = AtomicFile.ReadJson<ManagedTerraform>(Path.Combine(directory, ManifestName), IntegrationJson.Options,
                m => m.Version == name && !string.IsNullOrEmpty(m.BinarySha256));
            // O caminho vem da pasta, nunca do manifesto: um manifesto adulterado não aponta o Spock para outro binário.
            if (manifest is not null) found.Add(manifest with { Path = Path.Combine(directory, Platform.BinaryName) });
        }
        return found;
    }

    public bool Intact(ManagedTerraform installation) =>
        File.Exists(installation.Path) && string.Equals(HashFile(installation.Path), installation.BinarySha256, StringComparison.Ordinal);

    public async Task<IReadOnlyList<TerraformRelease>> ReleasesAsync(CancellationToken ct)
    {
        if (Platform is null) return [];
        // A API devolve no máximo 20 por página, da mais nova para a mais velha; versões antigas vêm por ReleaseAsync.
        var json = await GetSmallAsync(new Uri($"{ReleasesApi}?limit=20"), ct);
        return (JsonNode.Parse(Encoding.UTF8.GetString(json)) as JsonArray ?? [])
            .Select(node => ToRelease(node as JsonObject))
            .OfType<TerraformRelease>()
            .ToList();
    }

    public async Task<TerraformRelease?> ReleaseAsync(string version, CancellationToken ct)
    {
        if (Platform is null || TerraformVersion.TryParse(version) is null) return null;
        try
        {
            var json = await GetSmallAsync(new Uri($"{ReleasesApi}/{Uri.EscapeDataString(version)}"), ct);
            return ToRelease(JsonNode.Parse(Encoding.UTF8.GetString(json)) as JsonObject);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ManagedTerraform> InstallAsync(TerraformRelease release, CancellationToken ct)
    {
        var platform = Platform ?? throw IacErrors.UnsupportedPlatform();
        if (TerraformVersion.TryParse(release.Version) is not { IsPrerelease: false }) throw IacErrors.VersionInvalid(release.Version);
        var archiveName = $"terraform_{release.Version}_{platform.Key}.zip";
        foreach (var url in new[] { release.ArchiveUrl, release.ShasumsUrl, release.SignatureUrl })
            if (!Official(url, release.Version)) throw IacErrors.DownloadFailed();
        if (!release.ArchiveUrl.EndsWith("/" + archiveName, StringComparison.Ordinal)) throw IacErrors.ArchiveInvalid();

        Directory.CreateDirectory(_root);
        var staging = Path.Combine(_root, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var sums = await DownloadSmallAsync(release.ShasumsUrl, ct);
            var signature = await DownloadSmallAsync(release.SignatureUrl, ct);
            var signedBy = _keyring.Verify(sums, signature) ?? throw IacErrors.SignatureInvalid();
            var expected = ExpectedSha256(Encoding.UTF8.GetString(sums), archiveName) ?? throw IacErrors.ChecksumMismatch();

            var archive = Path.Combine(staging, archiveName);
            var archiveSha = await DownloadArchiveAsync(release.ArchiveUrl, archive, ct);
            if (!string.Equals(archiveSha, expected, StringComparison.Ordinal)) throw IacErrors.ChecksumMismatch();

            var binary = Path.Combine(staging, platform.BinaryName);
            ExtractBinary(archive, platform.BinaryName, binary);
            File.Delete(archive);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var installed = new ManagedTerraform(release.Version, "", platform.Os, platform.Arch, archiveSha, HashFile(binary), release.ArchiveUrl,
                signedBy, DateTimeOffset.UtcNow);
            AtomicFile.Write(Path.Combine(staging, ManifestName), JsonSerializer.Serialize(installed, IntegrationJson.Options));

            var target = Path.Combine(_root, release.Version);
            // Uma pasta sem manifesto válido é sobra de tentativa anterior; com manifesto válido, a versão já estava lá.
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
            return installed with { Path = Path.Combine(target, platform.BinaryName) };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            throw ex is InvalidDataException ? IacErrors.ArchiveInvalid() : IacErrors.DownloadFailed();
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public void Remove(string version)
    {
        // Só nomes de versão: nada de "..", barra ou caminho vindo de fora.
        if (TerraformVersion.TryParse(version) is null || version.Contains('/') || version.Contains('\\')) throw IacErrors.VersionInvalid(version);
        var directory = Path.Combine(_root, version);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    public string? FindExternal()
    {
        var found = _locator.Find("terraform");
        if (found is null) return null;
        var full = Path.GetFullPath(found);
        return full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) ? null : full;
    }

    public string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public void Dispose() => _http.Dispose();

    public static TerraformPlatform? Detect()
    {
        var os = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsMacOS() ? "darwin"
            : OperatingSystem.IsLinux() ? "linux"
            : null;
        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 when os is "windows" or "linux" => "386",
            Architecture.Arm when os is "linux" => "arm",
            _ => null
        };
        return os is null || arch is null ? null : new TerraformPlatform(os, arch);
    }

    private TerraformRelease? ToRelease(JsonObject? node)
    {
        if (node is null || Platform is null) return null;
        var version = node["version"]?.GetValue<string>();
        if (version is null || node["is_prerelease"]?.GetValue<bool>() != false) return null;
        var archive = (node["builds"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .FirstOrDefault(b => b["os"]?.GetValue<string>() == Platform.Os && b["arch"]?.GetValue<string>() == Platform.Arch)?["url"]?.GetValue<string>();
        var sums = node["url_shasums"]?.GetValue<string>();
        var signatures = (node["url_shasums_signatures"] as JsonArray ?? []).Select(s => s?.GetValue<string>()).OfType<string>().ToList();
        // A assinatura nomeada pela chave do chaveiro, e a genérica se só ela existir.
        var shortId = SigningKey[^8..];
        var signature = signatures.FirstOrDefault(s => s.EndsWith($".{shortId}.sig", StringComparison.OrdinalIgnoreCase)) ?? signatures.FirstOrDefault();
        if (archive is null || sums is null || signature is null) return null;
        if (!Official(archive, version) || !Official(sums, version) || !Official(signature, version)) return null;
        var published = DateTimeOffset.TryParse(node["timestamp_created"]?.GetValue<string>(), out var at) ? at : DateTimeOffset.MinValue;
        var changelog = node["url_changelog"]?.GetValue<string>();
        return new TerraformRelease(version, published, archive, sums, signature,
            changelog is not null && changelog.StartsWith("https://", StringComparison.Ordinal) ? changelog : null);
    }

    private static bool Official(string url, string version) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, DownloadHost, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith($"/terraform/{version}/", StringComparison.Ordinal);

    private static string? ExpectedSha256(string sums, string archiveName) =>
        sums.Split('\n')
            .Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts.Length == 2 && parts[1] == archiveName && parts[0].Length == 64)?[0].ToLowerInvariant();

    // Só o binário, pelo nome exato na raiz do pacote: nenhum outro caminho do zip chega ao disco.
    private static void ExtractBinary(string archive, string binaryName, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName == binaryName) ?? throw IacErrors.ArchiveInvalid();
        if (entry.Length <= 0 || entry.Length > MaxArchiveBytes) throw IacErrors.ArchiveInvalid();
        entry.ExtractToFile(destination, overwrite: false);
    }

    private async Task<byte[]> DownloadSmallAsync(string url, CancellationToken ct) => await GetSmallAsync(new Uri(url), ct);

    private async Task<byte[]> GetSmallAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxSmallFileBytes) throw new IOException("Resposta maior que o esperado.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private async Task<string> DownloadArchiveAsync(string url, string destination, CancellationToken ct)
    {
        using var response = await _http.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxArchiveBytes) throw new IOException("Pacote maior que o esperado.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            total += read;
            if (total > MaxArchiveBytes) throw new IOException("Pacote maior que o esperado.");
            hash.AppendData(chunk, 0, read);
            await target.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
