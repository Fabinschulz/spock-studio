using System.Text;
using System.Text.Json.Nodes;
using Mariliz.Application.Iac;
using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure.Terraform;

// O Terraform da HashiCorp, só de releases.hashicorp.com e sem redirecionamento. BUSL 1.1: gratuito para a própria
// infraestrutura; oferecer o Terraform como serviço a terceiros pede revisão da licença.
public sealed class HashiCorpTerraformToolchain : OfficialReleaseToolchain
{
    public const string ReleasesApi = "https://api.releases.hashicorp.com/v1/releases/terraform";
    public const string DownloadHost = "releases.hashicorp.com";

    private static readonly Lazy<OpenPgpKeyring> Keyring = new(() => EmbeddedKeyring("Mariliz.Infrastructure.Terraform.hashicorp-release-key.asc"));

    public HashiCorpTerraformToolchain(string dataRoot, IExecutableLocator locator, HttpMessageHandler? handler = null,
        TerraformPlatform? platform = null, OpenPgpKeyring? keyring = null)
        : base(dataRoot, "terraform", locator, keyring ?? Keyring.Value, handler, platform)
    {
    }

    public override IacEngine Engine => IacEngine.Terraform;

    public override string Name => "Terraform";

    public override string License => "BUSL 1.1 (HashiCorp)";

    protected override string BinaryBaseName => "terraform";

    public override async Task<IReadOnlyList<TerraformRelease>> ReleasesAsync(CancellationToken ct)
    {
        if (Platform is null) return [];
        // A API devolve no máximo 20 por página, da mais nova para a mais velha; versões antigas vêm por ReleaseAsync.
        var json = await GetSmallAsync(new Uri($"{ReleasesApi}?limit=20"), MaxListingBytes, ct);
        return (JsonNode.Parse(Encoding.UTF8.GetString(json)) as JsonArray ?? [])
            .Select(node => ToRelease(node as JsonObject))
            .OfType<TerraformRelease>()
            .ToList();
    }

    public override async Task<TerraformRelease?> ReleaseAsync(string version, CancellationToken ct)
    {
        if (Platform is null || TerraformVersion.TryParse(version) is null) return null;
        try
        {
            var json = await GetSmallAsync(new Uri($"{ReleasesApi}/{Uri.EscapeDataString(version)}"), MaxListingBytes, ct);
            return ToRelease(JsonNode.Parse(Encoding.UTF8.GetString(json)) as JsonObject);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    protected override bool Official(string url, string version) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, DownloadHost, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith($"/terraform/{version}/", StringComparison.Ordinal);

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
        DateTimeOffset? published = DateTimeOffset.TryParse(node["timestamp_created"]?.GetValue<string>(), out var at) ? at : null;
        var changelog = node["url_changelog"]?.GetValue<string>();
        return new TerraformRelease(IacEngine.Terraform, version, published, archive, sums, signature,
            changelog is not null && changelog.StartsWith("https://", StringComparison.Ordinal) ? changelog : null);
    }
}
