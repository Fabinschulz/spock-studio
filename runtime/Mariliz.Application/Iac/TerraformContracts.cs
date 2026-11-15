using System.Text.Json.Serialization;

namespace Mariliz.Application.Iac;

// Os nomes de sistema e arquitetura que a HashiCorp usa nos arquivos de cada versão (terraform_<v>_<os>_<arch>.zip).
public sealed record TerraformPlatform(string Os, string Arch)
{
    public string Key => $"{Os}_{Arch}";

    [JsonIgnore]
    public string BinaryName => Os == "windows" ? "terraform.exe" : "terraform";
}

// Uma versão publicada e os endereços oficiais do pacote desta plataforma, da lista de checksums e da assinatura dela.
public sealed record TerraformRelease(
    string Version,
    DateTimeOffset PublishedAt,
    string ArchiveUrl,
    string ShasumsUrl,
    string SignatureUrl,
    string? ChangelogUrl);

// O manifesto de uma instalação gerenciada: o hash do binário é conferido de novo antes de cada uso.
public sealed record ManagedTerraform(
    string Version,
    string Path,
    string Os,
    string Arch,
    string ArchiveSha256,
    string BinarySha256,
    string SourceUrl,
    string SignedBy,
    DateTimeOffset InstalledAt);

public enum TerraformSource
{
    Managed,
    External
}

// Um Terraform que a pessoa instalou fora do Spock e escolheu usar. Vale só enquanto o binário tiver o mesmo hash.
public sealed record AdoptedTerraform(string Path, string Version, string BinarySha256, DateTimeOffset AdoptedAt);

public sealed record ProjectTerraformPin(string Version, TerraformSource Source, DateTimeOffset PinnedAt);

// O que a pessoa decidiu: recusa da instalação, instalações externas adotadas e a versão fixada por projeto.
public sealed record TerraformSettings(
    DateTimeOffset? DeclinedAt,
    List<AdoptedTerraform> Adopted,
    Dictionary<string, ProjectTerraformPin> Pins)
{
    public static TerraformSettings Empty => new(null, [], []);
}

public sealed record TerraformInstallation(
    TerraformSource Source,
    string Version,
    string Path,
    bool Compatible,
    bool Verified,
    bool Adopted,
    string? Problem,
    DateTimeOffset? InstalledAt);

public sealed record TerraformRequirement(string File, string Constraint, bool Valid);

public sealed record ProjectTerraform(
    string ProjectId,
    IReadOnlyList<TerraformRequirement> Requirements,
    ProjectTerraformPin? Pin,
    TerraformInstallation? Selected,
    bool Automatic,
    string? Problem);

public enum TerraformState
{
    // Há uma instalação verificada e compatível pronta para uso.
    Ready,
    // Há um Terraform instalado fora do Spock, compatível, esperando a pessoa decidir se o Spock pode usá-lo.
    ExternalAvailable,
    // Nada instalado; o Spock pode oferecer a instalação.
    Missing,
    // A pessoa disse "Não instalar agora": nada é oferecido até ela pedir.
    Declined,
    // Há instalações, mas nenhuma atende ao mínimo do Spock e às exigências do projeto.
    Incompatible,
    // O sistema ou a arquitetura desta máquina não tem pacote oficial.
    UnsupportedPlatform
}

public sealed record TerraformStatus(
    TerraformState State,
    TerraformPlatform? Platform,
    string MinimumVersion,
    IReadOnlyList<TerraformInstallation> Installations,
    TerraformInstallation? Selected,
    ProjectTerraform? Project,
    DateTimeOffset? DeclinedAt,
    string Message);

// A proposta que a pessoa autoriza: o id amarra versão, plataforma, endereços e chave. Se algo mudar, a autorização não vale.
public sealed record TerraformOffer(
    string OfferId,
    string Version,
    TerraformPlatform Platform,
    string ArchiveUrl,
    string ShasumsUrl,
    string SignatureUrl,
    string SigningKey,
    DateTimeOffset PublishedAt,
    string? ChangelogUrl,
    string? CurrentVersion,
    bool IsUpdate,
    IReadOnlyList<string> Impacts);
