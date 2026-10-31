using Mariliz.Application.Execution;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Models;

/// <summary>
/// Proxy de modelo no motor. A chave não sai daqui para o runtime de IA, o journal ou a resposta.
/// Uma conclusão do modelo não é veredito: quem decide continua sendo a verificação determinística.
/// </summary>
public sealed class ModelGateway(IModelSettings settings, IModelTransport transport, TimeSpan? timeout = null)
{
    public const string Provider = "openai";
    public const int MaxTokens = 256;
    public static readonly Uri Endpoint = new("https://api.openai.com/v1/chat/completions");

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

    private static string Redact(string text, string key)
    {
        if (text.Length == 0) return text;
        if (key.Length > 0) text = text.Replace(key, "[omitido]", StringComparison.Ordinal);
        return SecretScanner.RedactLines(text);
    }
}
