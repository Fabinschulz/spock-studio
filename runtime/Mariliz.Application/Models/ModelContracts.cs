namespace Mariliz.Application.Models;

// Configured: há ao menos um provedor com chave. Provider: o padrão, que vale para quem não escolheu outro.
public sealed record ModelStatus(bool Configured, string Provider);

public sealed record ModelTransportResult(string Text, int Tokens);

public static class ModelKeyProtection
{
    public const string Vault = "vault";
    public const string File = "file";
}

// Nunca inclui a chave.
public sealed record ModelCheckError(string Code, string Message, DateTimeOffset At);

public sealed record ModelCheck(string Status, DateTimeOffset CheckedAt, ModelCheckError? Error);

// Guardado ao lado da chave mas sem a chave: é só estado, um por provedor.
public interface IModelCheckStore
{
    ModelCheck? Read(string provider);

    void Write(string provider, ModelCheck check);

    void Clear(string provider);
}

// Status: none (sem chave), untested (nunca conferida), connected ou error. Nunca inclui a chave.
/// <param name="Model">O modelo que este provedor usa quando ninguém escolheu outro.</param>
public sealed record ProviderConnection(
    string Id,
    string Name,
    bool Configured,
    bool IsDefault,
    string Status,
    string? Protection,
    DateTimeOffset? CheckedAt,
    ModelCheckError? LastError,
    string Model,
    IReadOnlyList<ModelOption> Models);

public sealed record ModelConnection(
    bool Configured,
    string Provider,
    IReadOnlyList<ProviderConnection> Providers,
    IReadOnlyList<ModelPurposeState> Purposes);

/// <param name="Provider">De quem é o modelo: o provedor sai do próprio modelo.</param>
/// <param name="IsDefault">Ninguém escolheu um modelo para a finalidade: vale o padrão do provedor padrão.</param>
public sealed record ModelPurposeState(string Id, string Label, string Description, string Provider, string Model, bool IsDefault);

// Só no diretório de dados do motor. Cada provedor tem a sua chave; guardar uma nunca mexe nas dos outros.
public interface IModelSettings
{
    ModelStatus Status();

    // Antes de qualquer chamada ao provedor. Sem regra, não recusa nada.
    void Validate(string provider, string key) { }

    string? Protection() => null;

    void Save(string provider, string key);

    void Clear();

    bool TryGetKey(out string key);

    // Os padrões modelam o armazenamento de uma chave só: o único provedor guardado é também o padrão.
    IReadOnlyList<string> Providers() => Status() is { Configured: true } status ? [status.Provider] : [];

    bool TryGetKey(string provider, out string key)
    {
        key = "";
        return Status() is { Configured: true } status && status.Provider == provider && TryGetKey(out key);
    }

    string? Protection(string provider) => Status() is { Configured: true } status && status.Provider == provider ? Protection() : null;

    void Remove(string provider)
    {
        if (Status() is { Configured: true } status && status.Provider == provider) Clear();
    }

    void SetDefault(string provider) { }
}

// A implementação recusa qualquer destino fora da allowlist.
public interface IModelTransport
{
    Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct);
}

public sealed record ChatMessage(string Role, string Content);

public sealed record ChatRequest(IReadOnlyList<ChatMessage> Messages, int MaxTokens, bool JsonObject);

public sealed record ChatTransportResult(string Text, string ModelVersion, int InputTokens, int OutputTokens);

// PromptHash é o SHA-256 das mensagens como saíram, já sem segredos: prova do que o modelo recebeu.
public sealed record ChatCompletion(string Text, string Model, string ModelVersion, int InputTokens, int OutputTokens, string PromptHash);

// A implementação recusa destino fora da allowlist.
public interface IModelChatTransport
{
    Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct);
}
