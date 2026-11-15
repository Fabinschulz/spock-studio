using Mariliz.Application.Execution;

namespace Mariliz.Application.Cloud;

// Sem texto vindo do provedor: ARN de terceiros, nome de política ou cabeçalho não chegam à tela, ao MCP nem à trilha.
public static class CloudErrors
{
    public const string Prefix = "CLOUD_";
    public const string NotAuthenticatedCode = "CLOUD_NOT_AUTHENTICATED";
    public const string ExpiredCode = "CLOUD_CREDENTIALS_EXPIRED";
    public const string InvalidIdentityCode = "CLOUD_IDENTITY_INVALID";
    public const string PermissionDeniedCode = "CLOUD_PERMISSION_DENIED";
    public const string RegionInvalidCode = "CLOUD_REGION_INVALID";
    public const string UnavailableCode = "CLOUD_UNAVAILABLE";
    public const string ServiceDisabledCode = "CLOUD_SERVICE_DISABLED";

    public static EngineException NotAuthenticated(string hint) =>
        new(NotAuthenticatedCode, $"Não há credencial onde o SDK do provedor procura. {hint}");

    public static EngineException Expired(string hint) =>
        new(ExpiredCode, $"A credencial venceu. {hint}");

    public static EngineException InvalidIdentity(string message) => new(InvalidIdentityCode, message);

    public static EngineException PermissionDenied() =>
        new(PermissionDeniedCode, "A identidade não tem permissão para esta leitura.");

    public static EngineException RegionInvalid(string message) => new(RegionInvalidCode, message);

    public static EngineException Unavailable() =>
        new(UnavailableCode, "O provedor não respondeu. Tente de novo em alguns minutos.");

    public static EngineException ServiceDisabled() =>
        new(ServiceDisabledCode, "O serviço desta leitura não está habilitado na conta ou no projeto.");

    public static EngineException InvalidResponse() =>
        new("CLOUD_INVALID_RESPONSE", "O provedor respondeu fora do formato esperado.");

    public static EngineException SettingsInvalid(string message) => new("CLOUD_SETTINGS_INVALID", message);

    public static EngineException ProviderUnknown() => new("CLOUD_PROVIDER_UNKNOWN", "Não há conector para este provedor.");

    public static EngineException MechanismUnknown() =>
        new("CLOUD_SETTINGS_INVALID", "Este provedor não oferece esse mecanismo de autenticação.");

    public static EngineException CapabilityUnknown() => new("CLOUD_CAPABILITY_UNKNOWN", "Este provedor não tem essa leitura.");

    public static EngineException ConnectionNotFound() => new("CLOUD_CONNECTION_NOT_FOUND", "Conexão cloud não encontrada.");

    public static EngineException ConnectionInUse() =>
        new("CLOUD_CONNECTION_IN_USE", "Há projetos usando esta conexão. Desvincule-os antes de removê-la.");

    public static EngineException LinkNotFound() => new("CLOUD_LINK_NOT_FOUND", "Vínculo cloud não encontrado neste projeto.");

    public static EngineException AlreadyLinked() =>
        new("CLOUD_ALREADY_LINKED", "Esta conexão já está vinculada ao projeto. Ajuste o vínculo existente.");

    public static EngineException NotLinked() =>
        new("CLOUD_NOT_LINKED", "Esta conexão não está vinculada ao projeto.");

    public static EngineException CapabilityNotEnabled() =>
        new("CLOUD_CAPABILITY_NOT_ENABLED", "O projeto não liberou esta leitura para esta conexão.");

    public static EngineException AgentNotAllowed() =>
        new("CLOUD_AGENT_NOT_ALLOWED", "O projeto não liberou esta conexão para agentes.");

    public static EngineException LimitReached(string message) => new("CLOUD_LIMIT_REACHED", message);

    // O que o código diz sobre a conexão; o resto (serviço desabilitado, leitura negada) é da leitura, não da conexão.
    public static CloudConnectionStatus? StatusOf(string code) => code switch
    {
        NotAuthenticatedCode => CloudConnectionStatus.NotAuthenticated,
        ExpiredCode => CloudConnectionStatus.Expired,
        InvalidIdentityCode => CloudConnectionStatus.InvalidIdentity,
        RegionInvalidCode => CloudConnectionStatus.InvalidRegion,
        UnavailableCode => CloudConnectionStatus.Unavailable,
        _ => null
    };
}
