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
