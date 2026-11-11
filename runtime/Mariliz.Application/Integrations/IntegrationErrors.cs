using Mariliz.Application.Execution;

namespace Mariliz.Application.Integrations;

/// <summary>Erros das integrações: o mesmo código sempre com a mesma explicação, sem texto vindo do provedor.</summary>
public static class IntegrationErrors
{
    public const string Prefix = "INTEGRATION_";
    public const string AuthFailedCode = "INTEGRATION_AUTH_FAILED";

    public static EngineException VaultUnavailable() =>
        new("CREDENTIAL_VAULT_UNAVAILABLE", "Este sistema não oferece armazenamento protegido de credenciais. A conexão não foi criada.");

    public static EngineException ProviderUnknown() =>
        new("INTEGRATION_PROVIDER_UNKNOWN", "Não há conector para este provedor.");

    public static EngineException SettingsInvalid(string message) => new("INTEGRATION_SETTINGS_INVALID", message);

    public static EngineException SecretRejected() =>
        new("INTEGRATION_SECRET_REJECTED", "A credencial não tem o formato esperado.");

    public static EngineException ResourceInvalid(string message) => new("INTEGRATION_RESOURCE_INVALID", message);

    public static EngineException AuthFailed() =>
        new(AuthFailedCode, "O provedor recusou a credencial. Confira se ela está correta e ainda é válida.");

    public static EngineException Forbidden() =>
        new("INTEGRATION_FORBIDDEN", "A credencial não tem permissão para este recurso.");

    public static EngineException ResourceNotFound() =>
        new("INTEGRATION_RESOURCE_NOT_FOUND", "O recurso não existe ou a credencial não o alcança.");

    public static EngineException InvalidResponse() =>
        new("INTEGRATION_INVALID_RESPONSE", "O provedor respondeu fora do formato esperado.");

    public static EngineException Unavailable() =>
        new("INTEGRATION_UNAVAILABLE", "O provedor não respondeu. Tente de novo em alguns minutos.");

    public static EngineException RateLimited(DateTimeOffset until) =>
        new("INTEGRATION_RATE_LIMITED", $"O provedor limitou as chamadas desta credencial. Tente de novo depois de {until.UtcDateTime:HH:mm} UTC.");

    public static EngineException ConnectionNotFound() =>
        new("CONNECTION_NOT_FOUND", "Conexão não encontrada.");

    public static EngineException ConnectionDisconnected() =>
        new("CONNECTION_DISCONNECTED", "A conexão está sem credencial. Atualize a credencial para voltar a usá-la.");

    public static EngineException ConnectionInUse() =>
        new("CONNECTION_IN_USE", "Há projetos usando esta conexão. Desvincule-os antes de removê-la.");

    public static EngineException LimitReached(string message) => new("INTEGRATION_LIMIT_REACHED", message);

    public static EngineException IntegrationNotFound() =>
        new("INTEGRATION_NOT_FOUND", "Integração não encontrada neste projeto.");

    public static EngineException AlreadyLinked() =>
        new("INTEGRATION_ALREADY_LINKED", "Este recurso já está vinculado ao projeto por esta conexão.");

    public static EngineException ScopeInvalid() =>
        new("INTEGRATION_SCOPE_INVALID", "Escolha ao menos um tipo de dado que o conector ofereça.");

    public static EngineException SyncRunning() =>
        new("INTEGRATION_SYNC_RUNNING", "Há uma sincronização em andamento nesta integração. Espere ela terminar.");
}
