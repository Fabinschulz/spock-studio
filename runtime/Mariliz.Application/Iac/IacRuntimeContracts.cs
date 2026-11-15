namespace Mariliz.Application.Iac;

// As duas ferramentas que entendem a mesma linguagem (HCL) e os mesmos providers. O OpenTofu é o padrão: gratuito e de
// código aberto (MPL 2.0). O Terraform da HashiCorp segue disponível para quem precisar dele (BUSL 1.1).
// Terraform é o 0 de propósito: manifesto e escolha gravados antes do OpenTofu existir no Spock eram todos do Terraform.
public enum IacEngine
{
    Terraform,
    OpenTofu
}

public sealed record IacEngineInfo(IacEngine Engine, string Name, string License, string MinimumVersion, bool Preferred, bool Available);

// Os nomes de sistema e arquitetura que as duas distribuições usam nos arquivos de cada versão (<ferramenta>_<v>_<os>_<arch>.zip).
public sealed record IacPlatform(string Os, string Arch)
{
    public string Key => $"{Os}_{Arch}";
}

// Uma versão publicada e os endereços oficiais do pacote desta plataforma, da lista de checksums e da assinatura dela.
public sealed record IacRelease(
    IacEngine Engine,
    string Version,
    DateTimeOffset? PublishedAt,
    string ArchiveUrl,
    string ShasumsUrl,
    string SignatureUrl,
    string? ChangelogUrl);

// O manifesto de uma instalação gerenciada: o hash do binário é conferido de novo antes de cada uso.
public sealed record ManagedRuntime(
    string Version,
    string Path,
    string Os,
    string Arch,
    string ArchiveSha256,
    string BinarySha256,
    string SourceUrl,
    string SignedBy,
    DateTimeOffset InstalledAt,
    IacEngine Engine = IacEngine.Terraform);

public enum IacRuntimeSource
{
    Managed,
    External
}

// Uma instalação que a pessoa fez fora do Spock e escolheu usar. Vale só enquanto o binário tiver o mesmo hash.
public sealed record AdoptedRuntime(string Path, string Version, string BinarySha256, DateTimeOffset AdoptedAt, IacEngine Engine = IacEngine.Terraform);

public sealed record ProjectRuntimePin(string Version, IacRuntimeSource Source, DateTimeOffset PinnedAt, IacEngine Engine = IacEngine.Terraform);

// O que a pessoa decidiu: recusa da instalação, instalações externas adotadas e a versão fixada por projeto.
public sealed record IacRuntimeSettings(
    DateTimeOffset? DeclinedAt,
    List<AdoptedRuntime> Adopted,
    Dictionary<string, ProjectRuntimePin> Pins)
{
    public static IacRuntimeSettings Empty => new(null, [], []);
}

public sealed record IacInstallation(
    IacEngine Engine,
    IacRuntimeSource Source,
    string Version,
    string Path,
    bool Compatible,
    bool Verified,
    bool Adopted,
    string? Problem,
    DateTimeOffset? InstalledAt);

public sealed record IacRequirement(string File, string Constraint, bool Valid);

public sealed record ProjectRuntime(
    string ProjectId,
    IReadOnlyList<IacRequirement> Requirements,
    ProjectRuntimePin? Pin,
    IacInstallation? Selected,
    bool Automatic,
    string? Problem);

public enum IacRuntimeState
{
    // Há uma instalação verificada e compatível pronta para uso.
    Ready,
    // Há uma instalação feita fora do Spock, compatível, esperando a pessoa decidir se o Spock pode usá-la.
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

public sealed record IacRuntimeStatus(
    IacRuntimeState State,
    IacPlatform? Platform,
    IReadOnlyList<IacEngineInfo> Engines,
    IReadOnlyList<IacInstallation> Installations,
    IacInstallation? Selected,
    ProjectRuntime? Project,
    DateTimeOffset? DeclinedAt,
    string Message);

// A proposta que a pessoa autoriza: o id amarra ferramenta, versão, plataforma, endereços e chave. Se algo mudar, a
// autorização não vale.
public sealed record IacRuntimeOffer(
    string OfferId,
    IacEngine Engine,
    string EngineName,
    string License,
    string Version,
    IacPlatform Platform,
    string ArchiveUrl,
    string ShasumsUrl,
    string SignatureUrl,
    string SigningKey,
    DateTimeOffset? PublishedAt,
    string? ChangelogUrl,
    string? CurrentVersion,
    bool IsUpdate,
    IReadOnlyList<string> Impacts);
