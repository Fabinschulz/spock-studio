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

    public static EngineException ManagedInstallDisabled(string engine, string? reason) =>
        new("IAC_MANAGED_INSTALL_DISABLED", reason ?? $"A instalação do {engine} pelo Spock está desativada nesta edição.");

    public static EngineException ExternalUseDisabled(string engine, string? reason) =>
        new("IAC_EXTERNAL_USE_DISABLED", reason ?? $"O uso de um {engine} instalado fora do Spock está desativado nesta edição.");

    public static EngineException ProjectIncompatible(string engine, string version, string constraint) =>
        new("IAC_PROJECT_INCOMPATIBLE", $"O projeto exige versão {constraint}; o {engine} {version} não atende.");

    public static EngineException FolderUnknown(string folder) =>
        new("IAC_FOLDER_UNKNOWN", $"A pasta {folder} não tem configuração de infraestrutura neste projeto.");

    public static EngineException NoConfiguration() =>
        new("IAC_NO_CONFIGURATION", "O projeto não tem arquivos .tf nem .tofu.");

    public static EngineException ConfigurationTooLarge() =>
        new("IAC_CONFIGURATION_TOO_LARGE", "A pasta de infraestrutura passa dos limites de cópia (5.000 arquivos, 200 MB). Separe-a do resto do projeto.");

    public static EngineException WorkspaceInvalid() =>
        new("IAC_WORKSPACE_INVALID", "O ambiente deve ter só letras minúsculas, dígitos, hífen e sublinhado (até 32 caracteres).");

    public static EngineException OperationRunning() =>
        new("IAC_OPERATION_RUNNING", "Já há uma operação de infraestrutura em andamento neste projeto.");

    public static EngineException OperationNotFound() =>
        new("IAC_OPERATION_NOT_FOUND", "Operação de infraestrutura não encontrada neste projeto.");

    public static EngineException ConnectionNotLinked() =>
        new("IAC_CONNECTION_NOT_LINKED", "Esta conexão cloud não está vinculada ao projeto. Vincule-a em Conexões cloud.");

    public static EngineException NotAPlan() =>
        new("IAC_NOT_A_PLAN", "Só um plano gerado com sucesso pode ser aprovado e aplicado.");

    public static EngineException PlanMismatch() =>
        new("IAC_PLAN_MISMATCH", "A aprovação não corresponde ao plano mostrado. Revise o plano e aprove de novo.");

    public static EngineException PlanNoChanges() =>
        new("IAC_PLAN_NO_CHANGES", "Este plano não tem mudanças: não há nada a aplicar.");

    public static EngineException ApplyBlocked(string reason) => new("IAC_APPLY_BLOCKED", reason);

    public static EngineException PlanAlreadyApplied() =>
        new("IAC_PLAN_ALREADY_APPLIED", "Este plano já foi aplicado. Gere um plano novo para outra mudança.");

    public static EngineException PlanSuperseded() =>
        new("IAC_PLAN_SUPERSEDED", "Há um plano mais novo para esta pasta e este ambiente. Aprove o mais recente.");

    public static EngineException PlanExpired() =>
        new("IAC_PLAN_EXPIRED", "O plano tem mais de 24 horas. Gere um plano novo antes de aplicar.");

    public static EngineException PlanChanged() =>
        new("IAC_PLAN_CHANGED", "O arquivo do plano ou a cópia da configuração mudou desde a geração. Nada foi aplicado.");

    public static EngineException PlanStale() =>
        new("IAC_PLAN_STALE", "A configuração do projeto mudou desde o plano. Gere um plano novo: a aprovação anterior não vale mais.");

    public static EngineException DestroyConfirmationRequired(int count) =>
        new("IAC_DESTROY_CONFIRMATION_REQUIRED", $"O plano destrói ou substitui {count} recurso(s). Confirme a destruição explicitamente.");

    public static EngineException RisksConfirmationRequired(int count) =>
        new("IAC_RISKS_CONFIRMATION_REQUIRED", $"O plano tem {count} achado(s) grave(s) de segurança. Confirme que revisou antes de aplicar.");

    public static EngineException WorkspaceConfirmationRequired(string workspace) =>
        new("IAC_WORKSPACE_CONFIRMATION_REQUIRED", $"Aplicar em {workspace} pede que você digite o nome do ambiente para confirmar.");

    public static EngineException RuntimeChanged() =>
        new("IAC_RUNTIME_CHANGED", "A ferramenta ou a versão do projeto mudou desde o plano. Gere um plano novo com a ferramenta atual.");

    public static EngineException StateChanged() =>
        new("IAC_STATE_CHANGED", "O state local do projeto mudou desde o plano. Gere um plano novo: aplicar o antigo apagaria a mudança.");

    public static EngineException StateConflict(string recovery) =>
        new("IAC_STATE_CONFLICT", $"O state do projeto mudou durante o apply e não foi sobrescrito. O state novo ficou guardado em {recovery}; " +
            "compare os dois antes de decidir qual fica.");

    public static EngineException StateShared(IEnumerable<string> projects) =>
        new("IAC_STATE_SHARED", $"Este state já é usado por outro projeto do Spock ({string.Join(", ", projects)}). Use uma chave própria por projeto e ambiente.");

    public static EngineException LockIdInvalid() =>
        new("IAC_LOCK_ID_INVALID", "O id da trava deve ser o que a ferramenta informou (letras, dígitos e hífen).");

    public static EngineException ProviderUnknown(string provider) =>
        new("IAC_PROVIDER_UNKNOWN", $"Não há bootstrap de state para {provider}. Use aws, azure ou gcp.");

    public static EngineException BootstrapValueInvalid(string message) => new("IAC_BOOTSTRAP_INVALID", message);

    public static EngineException BlueprintInvalid(IEnumerable<IacBlueprintCheck> failed) =>
        new("IAC_BLUEPRINT_INVALID", "O desenho não passou nas verificações: " + string.Join("; ", failed.Select(check => check.Description)) + ".");

    public static EngineException CostSourceUnavailable() =>
        new("IAC_COST_SOURCE_UNAVAILABLE", "A estimativa usa a lista de preços do provedor pela conexão cloud do projeto: vincule uma conexão da mesma nuvem " +
            "do desenho e libere nela a leitura de preços públicos.");

    public static EngineException GenerationFolderInvalid() =>
        new("IAC_GENERATION_FOLDER_INVALID", "A pasta da infraestrutura deve ser um caminho relativo dentro do projeto, com letras, dígitos, ponto, hífen e sublinhado.");

    public static EngineException UnlockConfirmationRequired() =>
        new("IAC_UNLOCK_CONFIRMATION_REQUIRED", "Liberar a trava do state pede confirmação: se alguém ainda estiver aplicando, o state pode corromper.");
}
