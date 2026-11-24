using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mariliz.Application.Iac;
using Mariliz.Application.Integrations;
using Mariliz.Application.Ports;
using Mariliz.Infrastructure.Integrations;

namespace Mariliz.Infrastructure.Iac;

// O que as duas distribuições (OpenTofu e Terraform) têm em comum: instalações em <dados>/tools/<ferramenta>/<versão>/,
// a lista de checksums com assinatura válida de uma chave do chaveiro embutido, o pacote batendo com ela e só o binário
// extraído. O binário nunca vai para o PATH. Cada subclasse diz onde a ferramenta publica as versões.
public abstract class OfficialReleaseToolchain : IIacToolchain, IDisposable
{
    private const string ManifestName = "install.json";
    private const long MaxArchiveBytes = 256L * 1024 * 1024;
    private const int MaxRedirects = 3;
    protected const int MaxListingBytes = 8 * 1024 * 1024;
    private const int MaxSmallFileBytes = 1024 * 1024;

    private readonly string _root;
    private readonly IExecutableLocator _locator;
    private readonly OpenPgpKeyring _keyring;
    private readonly HttpClient _http;

    protected OfficialReleaseToolchain(string dataRoot, string folder, IExecutableLocator locator, OpenPgpKeyring keyring,
        HttpMessageHandler? handler, IacPlatform? platform)
    {
        _root = Path.Combine(dataRoot, "tools", folder);
        _locator = locator;
        _keyring = keyring;
        Platform = platform ?? Detect();
        // Redirecionamento só pelas regras de cada distribuição (FollowAllowed): o pacote nunca vem de onde uma resposta mandar.
        _http = handler is null
            ? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
            : new HttpClient(handler, disposeHandler: false);
        _http.Timeout = IacTimeouts.Download;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SpockStudio");
    }

    public abstract IacEngine Engine { get; }

    public abstract string Name { get; }

    public abstract string License { get; }

    // O nome do executável sem extensão (tofu, terraform) e o prefixo dos arquivos publicados.
    protected abstract string BinaryBaseName { get; }

    public IacPlatform? Platform { get; }

    public string SigningKey => _keyring.PrimaryFingerprint;

    protected string BinaryName => Platform?.Os == "windows" ? BinaryBaseName + ".exe" : BinaryBaseName;

    protected string ArchiveName(string version) => $"{BinaryBaseName}_{version}_{Platform!.Key}.zip";

    public abstract Task<IReadOnlyList<IacRelease>> ReleasesAsync(CancellationToken ct);

    public abstract Task<IacRelease?> ReleaseAsync(string version, CancellationToken ct);

    // O endereço de um arquivo da versão na fonte oficial (antes de qualquer redirecionamento).
    protected abstract bool Official(string url, string version);

    // Para onde a fonte oficial pode mandar o download (a CDN dela); nada além disso é seguido.
    protected virtual bool RedirectAllowed(Uri target) => false;

    public IReadOnlyList<ManagedRuntime> Managed()
    {
        if (Platform is null || !Directory.Exists(_root)) return [];
        var found = new List<ManagedRuntime>();
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            var name = Path.GetFileName(directory);
            if (IacVersion.TryParse(name) is null) continue;
            var manifest = AtomicFile.ReadJson<ManagedRuntime>(Path.Combine(directory, ManifestName), IntegrationJson.Options,
                m => m.Version == name && !string.IsNullOrEmpty(m.BinarySha256));
            // O caminho e a ferramenta vêm da pasta, nunca do manifesto: um manifesto adulterado não aponta o Spock para outro binário.
            if (manifest is not null) found.Add(manifest with { Path = Path.Combine(directory, BinaryName), Engine = Engine });
        }
        return found;
    }

    public bool Intact(ManagedRuntime installation) =>
        File.Exists(installation.Path) && string.Equals(HashFile(installation.Path), installation.BinarySha256, StringComparison.Ordinal);

    public async Task<ManagedRuntime> InstallAsync(IacRelease release, CancellationToken ct)
    {
        var platform = Platform ?? throw IacErrors.UnsupportedPlatform(Name);
        if (release.Engine != Engine || IacVersion.TryParse(release.Version) is not { IsPrerelease: false }) throw IacErrors.VersionInvalid(Name, release.Version);
        var archiveName = ArchiveName(release.Version);
        foreach (var url in new[] { release.ArchiveUrl, release.ShasumsUrl, release.SignatureUrl })
            if (!Official(url, release.Version)) throw IacErrors.DownloadFailed(Name);
        if (!release.ArchiveUrl.EndsWith("/" + archiveName, StringComparison.Ordinal)) throw IacErrors.ArchiveInvalid();

        Directory.CreateDirectory(_root);
        var staging = Path.Combine(_root, ".staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var sums = await GetSmallAsync(new Uri(release.ShasumsUrl), MaxSmallFileBytes, ct);
            var signature = await GetSmallAsync(new Uri(release.SignatureUrl), MaxSmallFileBytes, ct);
            var signedBy = _keyring.Verify(sums, signature) ?? throw IacErrors.SignatureInvalid();
            var expected = ExpectedSha256(Encoding.UTF8.GetString(sums), archiveName) ?? throw IacErrors.ChecksumMismatch();

            var archive = Path.Combine(staging, archiveName);
            var archiveSha = await DownloadArchiveAsync(new Uri(release.ArchiveUrl), archive, ct);
            if (!string.Equals(archiveSha, expected, StringComparison.Ordinal)) throw IacErrors.ChecksumMismatch();

            var binary = Path.Combine(staging, BinaryName);
            ExtractBinary(archive, BinaryName, binary);
            File.Delete(archive);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var installed = new ManagedRuntime(release.Version, "", platform.Os, platform.Arch, archiveSha, HashFile(binary), release.ArchiveUrl,
                signedBy, DateTimeOffset.UtcNow, Engine);
            AtomicFile.Write(Path.Combine(staging, ManifestName), JsonSerializer.Serialize(installed, IntegrationJson.Options));

            var target = Path.Combine(_root, release.Version);
            // Pasta sem manifesto válido é sobra de tentativa anterior; com binário que falhou na verificação, é o que se conserta.
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
            return installed with { Path = Path.Combine(target, BinaryName) };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException
                                   || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            throw ex is InvalidDataException ? IacErrors.ArchiveInvalid() : IacErrors.DownloadFailed(Name);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public void Remove(string version)
    {
        // Só nomes de versão: nada de "..", barra ou caminho vindo de fora.
        if (IacVersion.TryParse(version) is null || version.Contains('/') || version.Contains('\\')) throw IacErrors.VersionInvalid(Name, version);
        var directory = Path.Combine(_root, version);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    public string? FindExternal()
    {
        var found = _locator.Find(BinaryBaseName);
        if (found is null) return null;
        var full = Path.GetFullPath(found);
        return full.StartsWith(Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase) ? null : full;
    }

    public string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public void Dispose()
    {
        _http.Dispose();
        GC.SuppressFinalize(this);
    }

    public static IacPlatform? Detect()
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
        return os is null || arch is null ? null : new IacPlatform(os, arch);
    }

    protected static OpenPgpKeyring EmbeddedKeyring(string resource)
    {
        using var stream = typeof(OfficialReleaseToolchain).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Chave {resource} ausente do pacote.");
        using var reader = new StreamReader(stream);
        return OpenPgpKeyring.FromArmored(reader.ReadToEnd());
    }

    protected async Task<byte[]> GetSmallAsync(Uri uri, int limit, CancellationToken ct)
    {
        using var response = await SendAsync(uri, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > limit) throw new IOException("Resposta maior que o esperado.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    // Segue no máximo três redirecionamentos, todos HTTPS e todos para destinos que a distribuição aceita.
    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken ct)
    {
        for (var hop = 0; ; hop++)
        {
            var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
                && response.Headers.Location is { } location)
            {
                response.Dispose();
                var target = location.IsAbsoluteUri ? location : new Uri(uri, location);
                if (hop >= MaxRedirects || target.Scheme != Uri.UriSchemeHttps || !RedirectAllowed(target))
                    throw new HttpRequestException("Redirecionamento recusado.");
                uri = target;
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException("Resposta sem sucesso.", null, status);
            }
            return response;
        }
    }

    private async Task<string> DownloadArchiveAsync(Uri uri, string destination, CancellationToken ct)
    {
        using var response = await SendAsync(uri, ct);
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
}
