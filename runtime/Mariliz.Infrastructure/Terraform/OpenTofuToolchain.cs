using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Terraform;

// O OpenTofu, projeto da Linux Foundation sob MPL 2.0: gratuito e de código aberto. A lista de versões vem de
// get.opentofu.org e os arquivos das releases do GitHub do projeto, que redirecionam para a CDN do GitHub. A lista de
// checksums é assinada com a chave GPG do OpenTofu (arquivo .gpgsig).
public sealed class OpenTofuToolchain : OfficialReleaseToolchain
{
    public const string VersionsApi = "https://get.opentofu.org/tofu/api.json";
    public const string ReleasesBase = "https://github.com/opentofu/opentofu/releases/download/";

    private static readonly Lazy<OpenPgpKeyring> Keyring = new(() => EmbeddedKeyring("Mariliz.Infrastructure.Terraform.opentofu-release-key.asc"));

    // A CDN onde o GitHub guarda os arquivos de release; o conteúdo é conferido pela assinatura, não pelo endereço.
    private static readonly string[] CdnHosts = ["release-assets.githubusercontent.com", "objects.githubusercontent.com"];

    public OpenTofuToolchain(string dataRoot, IExecutableLocator locator, HttpMessageHandler? handler = null,
        TerraformPlatform? platform = null, OpenPgpKeyring? keyring = null)
        : base(dataRoot, "opentofu", locator, keyring ?? Keyring.Value, handler, platform)
    {
    }

    public override IacEngine Engine => IacEngine.OpenTofu;

    public override string Name => "OpenTofu";

    public override string License => "MPL 2.0 (código aberto)";

    protected override string BinaryBaseName => "tofu";

    public override async Task<IReadOnlyList<TerraformRelease>> ReleasesAsync(CancellationToken ct)
    {
        if (Platform is null) return [];
        var json = await GetSmallAsync(new Uri(VersionsApi), MaxListingBytes, ct);
        return (JsonNode.Parse(Encoding.UTF8.GetString(json))?["versions"] as JsonArray ?? [])
            .Select(node => ToRelease(node as JsonObject))
            .OfType<TerraformRelease>()
            .ToList();
    }

    public override async Task<TerraformRelease?> ReleaseAsync(string version, CancellationToken ct) =>
        (await ReleasesAsync(ct)).FirstOrDefault(r => TerraformVersion.TryParse(r.Version)?.CompareTo(TerraformVersion.TryParse(version)) == 0);

    protected override bool Official(string url, string version) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith($"/opentofu/opentofu/releases/download/v{version}/", StringComparison.Ordinal);

    protected override bool RedirectAllowed(Uri target) =>
        CdnHosts.Contains(target.Host, StringComparer.OrdinalIgnoreCase);

    private TerraformRelease? ToRelease(JsonObject? node)
    {
        if (node is null || Platform is null) return null;
        var version = node["id"]?.GetValue<string>();
        if (TerraformVersion.TryParse(version) is not { IsPrerelease: false }) return null;
        var files = (node["files"] as JsonArray ?? []).Select(f => f?.GetValue<string>()).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var archive = ArchiveName(version!);
        var sums = $"tofu_{version}_SHA256SUMS";
        var signature = sums + ".gpgsig";
        if (!files.Contains(archive) || !files.Contains(sums) || !files.Contains(signature)) return null;
        string Url(string file) => $"{ReleasesBase}v{version}/{file}";
        return new TerraformRelease(IacEngine.OpenTofu, version!, null, Url(archive), Url(sums), Url(signature),
            $"https://github.com/opentofu/opentofu/blob/v{version}/CHANGELOG.md");
    }
}
