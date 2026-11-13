namespace Mariliz.Application.Models;

// Nunca inclui a chave.
public sealed record ModelStatus(bool Configured, string Provider);

public sealed record ModelCompletion(string Text, int Tokens, string Limitation);

public sealed record ModelTransportResult(string Text, int Tokens);

public static class ModelKeyProtection
{
    public const string Vault = "vault";
    public const string File = "file";
}

// Nunca inclui a chave.
public sealed record ModelCheckError(string Code, string Message, DateTimeOffset At);

public sealed record ModelCheck(string Status, DateTimeOffset CheckedAt, ModelCheckError? Error);

// Guardado ao lado da chave mas sem a chave: é só estado.
public interface IModelCheckStore
{
    ModelCheck? Read();

    void Write(ModelCheck check);

    void Clear();
}

// Status: none (sem chave), untested (nunca conferida), connected ou error. Nunca inclui a chave.
public sealed record ModelConnection(
    bool Configured,
    string Provider,
    string ProviderName,
    string Model,
    string Status,
    string? Protection,
    DateTimeOffset? CheckedAt,
    ModelCheckError? LastError,
    IReadOnlyList<ModelProviderInfo> Available,
    IReadOnlyList<ModelPurposeState> Purposes);

public sealed record ModelPurposeState(string Id, string Label, string Description, string Model, bool IsDefault);

// Só no diretório de dados do motor.
public interface IModelSettings
{
    ModelStatus Status();

    // Antes de qualquer chamada ao provedor. Sem regra, não recusa nada.
    void Validate(string provider, string key) { }

    string? Protection() => null;

    void Save(string provider, string key);

    void Clear();

    bool TryGetKey(out string key);
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
