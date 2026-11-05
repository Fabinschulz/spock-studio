namespace Mariliz.Application.Models;

/// <summary>Estado público do modelo. Nunca inclui a chave.</summary>
public sealed record ModelStatus(bool Configured, string Provider);

public sealed record ModelCompletion(string Text, int Tokens, string Limitation);

public sealed record ModelTransportResult(string Text, int Tokens);

/// <summary>Chave de um provedor, só no diretório de dados do motor.</summary>
public interface IModelSettings
{
    ModelStatus Status();

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
