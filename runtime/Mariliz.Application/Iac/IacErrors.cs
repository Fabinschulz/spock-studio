using Mariliz.Application.Execution;

namespace Mariliz.Application.Iac;

public static class IacErrors
{
    public static EngineException UnsupportedPlatform() =>
        new("IAC_PLATFORM_UNSUPPORTED", "A HashiCorp não publica o Terraform para o sistema e a arquitetura desta máquina.");

    public static EngineException VersionInvalid(string version) =>
        new("IAC_VERSION_INVALID", $"Versão do Terraform fora do formato: {version}.");

    public static EngineException VersionNotPublished(string version) =>
        new("IAC_VERSION_NOT_PUBLISHED", $"A versão {version} não está publicada como estável para esta máquina.");

    public static EngineException BelowMinimum(string version, string minimum) =>
        new("IAC_VERSION_INCOMPATIBLE", $"O Spock precisa do Terraform {minimum} ou mais novo; {version} não serve.");

    public static EngineException NoCompatibleRelease() =>
        new("IAC_NO_COMPATIBLE_RELEASE", "Nenhuma versão publicada atende ao mínimo do Spock e às exigências do projeto.");

    public static EngineException OfferStale() =>
        new("IAC_OFFER_STALE", "A proposta de instalação mudou desde que foi mostrada. Revise a nova proposta e autorize de novo.");

    public static EngineException DownloadFailed() =>
        new("IAC_DOWNLOAD_FAILED", "Não foi possível baixar o Terraform da HashiCorp. Confira a conexão e tente de novo.");

    public static EngineException SignatureInvalid() =>
        new("IAC_SIGNATURE_INVALID", "A lista de checksums não tem assinatura válida da HashiCorp. Nada foi instalado.");

    public static EngineException ChecksumMismatch() =>
        new("IAC_CHECKSUM_MISMATCH", "O pacote baixado não confere com o checksum assinado pela HashiCorp. Nada foi instalado.");

    public static EngineException ArchiveInvalid() =>
        new("IAC_ARCHIVE_INVALID", "O pacote baixado não tem o binário do Terraform. Nada foi instalado.");

    public static EngineException NotInstalled(string version) =>
        new("IAC_NOT_INSTALLED", $"O Terraform {version} não está instalado pelo Spock.");

    public static EngineException InUse(string version, IEnumerable<string> projects) =>
        new("IAC_VERSION_IN_USE", $"O Terraform {version} está fixado em {string.Join(", ", projects)}. Troque a versão desses projetos antes de remover.");

    public static EngineException ConfirmationRequired() =>
        new("IAC_CONFIRMATION_REQUIRED", "Remover uma versão pede confirmação explícita.");

    public static EngineException ExternalNotFound() =>
        new("IAC_EXTERNAL_NOT_FOUND", "Não há Terraform instalado fora do Spock nesse caminho.");

    public static EngineException ExternalChanged() =>
        new("IAC_EXTERNAL_CHANGED", "O Terraform externo mudou desde que foi escolhido. Confira a versão e escolha usá-lo de novo.");

    public static EngineException NotAvailable(string reason) => new("IAC_TERRAFORM_UNAVAILABLE", reason);

    public static EngineException ProjectIncompatible(string version, string constraint) =>
        new("IAC_PROJECT_INCOMPATIBLE", $"O projeto exige Terraform {constraint}; {version} não atende.");
}
