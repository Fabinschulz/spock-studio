namespace Mariliz.Application.Models;

/// <summary>Estado público do modelo. Nunca inclui a chave.</summary>
public sealed record ModelStatus(bool Configured, string Provider);

public sealed record ModelCompletion(string Text, int Tokens, string Limitation);

public sealed record ModelTransportResult(string Text, int Tokens);

/// <summary>Onde a chave está guardada: no cofre protegido pelo sistema ou num arquivo do diretório de dados do motor.</summary>
public static class ModelKeyProtection
{
    public const string Vault = "vault";
    public const string File = "file";
}

/// <summary>O que a última conferência da chave com o provedor encontrou. Nunca inclui a chave.</summary>
public sealed record ModelCheckError(string Code, string Message, DateTimeOffset At);

/// <param name="Status"><c>connected</c> (o provedor aceitou a chave) ou <c>error</c>.</param>
public sealed record ModelCheck(string Status, DateTimeOffset CheckedAt, ModelCheckError? Error);

/// <summary>O resultado da última conferência, guardado ao lado da chave mas sem a chave: é só estado.</summary>
public interface IModelCheckStore
{
    ModelCheck? Read();

    void Write(ModelCheck check);

    void Clear();
}

/// <summary>
/// O provedor de modelo como uma conexão: se há chave, de qual provedor, onde ela está guardada, qual modelo as conversas usam e o
/// que a última conferência com o provedor encontrou. <see cref="Available"/> são os provedores que esta versão sabe chamar. <see cref="Status"/>: <c>none</c> (sem chave), <c>untested</c> (nunca conferida),
/// <c>connected</c> ou <c>error</c>. Nunca inclui a chave.
/// </summary>
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

/// <summary>O modelo de uma finalidade hoje: o escolhido pela pessoa ou, sem escolha, o padrão do provedor (<see cref="IsDefault"/>).</summary>
public sealed record ModelPurposeState(string Id, string Label, string Description, string Model, bool IsDefault);

/// <summary>Chave de um provedor, só no diretório de dados do motor.</summary>
public interface IModelSettings
{
    ModelStatus Status();

    /// <summary>Recusa provedor fora da lista e chave fora do formato, antes de qualquer chamada ao provedor. Sem regra, não recusa nada.</summary>
    void Validate(string provider, string key) { }

    /// <summary>Onde a chave está guardada (<see cref="ModelKeyProtection"/>); nulo sem chave ou sem como saber.</summary>
    string? Protection() => null;

    void Save(string provider, string key);

    void Clear();

    bool TryGetKey(out string key);
}

/// <summary>Envio ao provedor. A implementação recusa qualquer destino fora da allowlist.</summary>
public interface IModelTransport
{
    Task<ModelTransportResult> SendAsync(Uri endpoint, string key, string prompt, int maxTokens, CancellationToken ct);
}

public sealed record ChatMessage(string Role, string Content);

/// <summary>Uma conversa com o modelo. <see cref="JsonObject"/> pede ao provedor uma resposta em JSON.</summary>
public sealed record ChatRequest(IReadOnlyList<ChatMessage> Messages, int MaxTokens, bool JsonObject);

public sealed record ChatTransportResult(string Text, string ModelVersion, int InputTokens, int OutputTokens);

/// <summary><see cref="PromptHash"/> é o SHA-256 das mensagens como saíram, já sem segredos: prova do que o modelo recebeu.</summary>
public sealed record ChatCompletion(string Text, string Model, string ModelVersion, int InputTokens, int OutputTokens, string PromptHash);

/// <summary>Envio de uma conversa ao provedor, com o uso real de tokens. A implementação recusa destino fora da allowlist.</summary>
public interface IModelChatTransport
{
    Task<ChatTransportResult> SendChatAsync(Uri endpoint, string key, string model, ChatRequest request, CancellationToken ct);
}
