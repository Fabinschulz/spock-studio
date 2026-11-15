using Mariliz.Application.Execution;

namespace Mariliz.Application.Iac;

// Cada mensagem diz a ferramenta (OpenTofu ou Terraform) para a pessoa saber de qual instalação se trata.
public static class IacErrors
{
    public static EngineException UnsupportedPlatform(string engine) =>
        new("IAC_PLATFORM_UNSUPPORTED", $"Não há pacote oficial do {engine} para o sistema e a arquitetura desta máquina.");

    public static EngineException VersionInvalid(string engine, string version) =>
        new("IAC_VERSION_INVALID", $"Versão do {engine} fora do formato: {version}.");

    public static EngineException VersionNotPublished(string engine, string version) =>
        new("IAC_VERSION_NOT_PUBLISHED", $"A versão {version} do {engine} não está publicada como estável para esta máquina.");

    public static EngineException BelowMinimum(string engine, string version, string minimum) =>
        new("IAC_VERSION_INCOMPATIBLE", $"O Spock precisa do {engine} {minimum} ou mais novo; {version} não serve.");

    public static EngineException NoCompatibleRelease(string engine) =>
        new("IAC_NO_COMPATIBLE_RELEASE", $"Nenhuma versão publicada do {engine} atende ao mínimo do Spock e às exigências do projeto.");

    public static EngineException OfferStale() =>
        new("IAC_OFFER_STALE", "A proposta de instalação mudou desde que foi mostrada. Revise a nova proposta e autorize de novo.");

    public static EngineException DownloadFailed(string engine) =>
        new("IAC_DOWNLOAD_FAILED", $"Não foi possível baixar o {engine} da fonte oficial. Confira a conexão e tente de novo.");

    public static EngineException SignatureInvalid() =>
        new("IAC_SIGNATURE_INVALID", "A lista de checksums não tem assinatura válida de quem publica a ferramenta. Nada foi instalado.");

    public static EngineException ChecksumMismatch() =>
        new("IAC_CHECKSUM_MISMATCH", "O pacote baixado não confere com o checksum assinado. Nada foi instalado.");

    public static EngineException ArchiveInvalid() =>
        new("IAC_ARCHIVE_INVALID", "O pacote baixado não tem o binário esperado. Nada foi instalado.");

    public static EngineException NotInstalled(string engine, string version) =>
        new("IAC_NOT_INSTALLED", $"O {engine} {version} não está instalado pelo Spock.");

    public static EngineException InUse(string engine, string version, IEnumerable<string> projects) =>
        new("IAC_VERSION_IN_USE", $"O {engine} {version} está fixado em {string.Join(", ", projects)}. Troque a versão desses projetos antes de remover.");

    public static EngineException ConfirmationRequired() =>
        new("IAC_CONFIRMATION_REQUIRED", "Remover uma versão pede confirmação explícita.");

    public static EngineException ExternalNotFound(string engine) =>
        new("IAC_EXTERNAL_NOT_FOUND", $"Não há {engine} instalado fora do Spock nesta máquina.");

    public static EngineException ExternalChanged(string engine) =>
        new("IAC_EXTERNAL_CHANGED", $"O {engine} desta máquina não é o que você escolheu (versão ou binário mudou). Confira e escolha usá-lo de novo.");

    public static EngineException NotAvailable(string reason) => new("IAC_TERRAFORM_UNAVAILABLE", reason);

    public static EngineException ProjectIncompatible(string engine, string version, string constraint) =>
        new("IAC_PROJECT_INCOMPATIBLE", $"O projeto exige versão {constraint}; o {engine} {version} não atende.");
}
