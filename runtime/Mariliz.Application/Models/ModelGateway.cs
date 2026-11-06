using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Models;

/// <summary>
/// Proxy de modelo no motor. A chave não sai daqui para o runtime de IA, o journal ou a resposta.
/// Uma conclusão do modelo não é veredito: quem decide continua sendo a verificação determinística.
/// </summary>
public sealed class ModelGateway(IModelSettings settings, IModelTransport transport, TimeSpan? timeout = null, IModelChatTransport? chat = null)
{
    public const string Provider = "openai";
    public const int MaxTokens = 256;

    /// <summary>Modelo padrão das conversas dos agentes (D4: só OpenAI). O agente não o escolhe.</summary>
    public const string DefaultModel = "gpt-4o-mini";

    /// <summary>Teto de saída por chamada de um agente e de texto na resposta.</summary>
    public const int MaxChatTokens = 8192;
    public const int MaxChatText = 64000;
    public static readonly Uri Endpoint = new("https://api.openai.com/v1/chat/completions");

    private static readonly TimeSpan ChatTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    public ModelStatus Status() => settings.Status();

    public void Save(string provider, string key) => settings.Save(provider, key);

    public void Clear() => settings.Clear();

    public async Task<ModelCompletion> CompleteAsync(string prompt, CancellationToken ct)
    {
        if (!settings.TryGetKey(out var key))
            return new ModelCompletion("", 0, "MODEL_NOT_CONFIGURED");

        prompt = Redact(prompt, key).Trim();
        if (prompt.Length == 0)
            return new ModelCompletion("", 0, "MODEL_PROMPT_EMPTY");
        if (prompt.Length > 4000) prompt = prompt[..4000];

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_timeout);
        ModelTransportResult result;
        try
        {
            result = await transport.SendAsync(Endpoint, key, prompt, MaxTokens, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EngineException("MODEL_TIMEOUT", "O modelo não respondeu no prazo.");
        }

        var text = Redact(result.Text, key);
        if (text.Length > 4000) text = text[..4000];
        var tokens = Math.Clamp(result.Tokens, 0, MaxTokens);
        return new ModelCompletion(text, tokens, "");
    }

    /// <summary>
    /// Conversa de um agente. As mensagens saem sem a chave nem formato de segredo, e a resposta volta pela mesma redação.
    /// Falha com <see cref="EngineException"/> (<c>MODEL_NOT_CONFIGURED</c>, <c>MODEL_TIMEOUT</c>); quem cobra o orçamento é o chamador.
    /// </summary>
    public async Task<ChatCompletion> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        if (chat is null || !settings.TryGetKey(out var key))
            throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhum modelo está configurado.");

        var messages = request.Messages.Select(m => m with { Content = Redact(m.Content, key) }).ToList();
        var bounded = request with { Messages = messages, MaxTokens = Math.Clamp(request.MaxTokens, 1, MaxChatTokens) };

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(ChatTimeout);
        ChatTransportResult result;
        try
        {
            result = await chat.SendChatAsync(Endpoint, key, DefaultModel, bounded, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EngineException("MODEL_TIMEOUT", "O modelo não respondeu no prazo.");
        }

        var text = Redact(result.Text, key);
        if (text.Length > MaxChatText) text = text[..MaxChatText];
        return new ChatCompletion(text, DefaultModel, result.ModelVersion, Math.Max(0, result.InputTokens), Math.Max(0, result.OutputTokens), HashOf(messages));
    }

    private static string HashOf(IReadOnlyList<ChatMessage> messages)
    {
        var json = JsonSerializer.Serialize(messages.Select(m => new { role = m.Role, content = m.Content }));
        return Hashing.Sha256(json);
    }

    private static string Redact(string text, string key)
    {
        if (text.Length == 0) return text;
        if (key.Length > 0) text = text.Replace(key, "[omitido]", StringComparison.Ordinal);
        return SecretScanner.RedactLines(text);
    }
}
