using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Models;

/// <summary>
/// Proxy de modelo no motor. A chave não sai daqui para o runtime de IA, o journal ou a resposta.
/// Uma conclusão do modelo não é veredito: quem decide continua sendo a verificação determinística.
/// </summary>
public sealed class ModelGateway(
    IModelSettings settings,
    IModelTransport transport,
    TimeSpan? timeout = null,
    IModelChatTransport? chat = null,
    IModelCheckStore? checks = null,
    IClock? clock = null,
    IModelPreferences? preferences = null)
{
    /// <summary>O provedor de quem já tinha uma chave antes de haver escolha. Quem decide o provedor de hoje é <see cref="ModelProviders"/>.</summary>
    public const string Provider = "openai";
    public const int MaxTokens = 256;

    /// <summary>Modelo padrão da OpenAI, o provedor padrão. Cada provedor tem o seu em <see cref="ModelProviders"/>; o agente não o escolhe.</summary>
    public const string DefaultModel = "gpt-4o-mini";

    /// <summary>Teto de saída por chamada de um agente e de texto na resposta.</summary>
    public const int MaxChatTokens = 8192;
    public const int MaxChatText = 64000;
    public static readonly Uri Endpoint = ModelProviders.OpenAi.Endpoint;

    private static readonly TimeSpan ChatTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    /// <summary>A pergunta curta que confere a chave: o provedor só precisa aceitá-la, a resposta não é usada.</summary>
    public const string ProbePrompt = "Responda apenas: ok";

    public const int ProbeMaxTokens = 8;

    private DateTimeOffset Now => clock?.UtcNow ?? DateTimeOffset.UtcNow;

    public ModelStatus Status() => settings.Status();

    public void Save(string provider, string key)
    {
        settings.Save(provider, key);
        checks?.Clear();
    }

    public void Clear()
    {
        settings.Clear();
        checks?.Clear();
        preferences?.Clear();
    }

    /// <summary>O provedor como uma conexão: se há chave, onde está guardada e o que a última conferência encontrou.</summary>
    public ModelConnection Connection()
    {
        var status = settings.Status();
        var available = ModelProviders.Available();
        if (!status.Configured) return new ModelConnection(false, "", "", "", "none", null, null, null, available, []);

        var provider = Active(status);
        var check = checks?.Read();
        return new ModelConnection(true, provider.Id, provider.Name, provider.DefaultModel, check?.Status ?? "untested", settings.Protection(), check?.CheckedAt, check?.Error, available, Purposes(provider));
    }

    /// <summary>
    /// Guarda a chave só depois de o provedor a aceitar, como numa conexão de integração: formato conferido, uma pergunta curta ao
    /// provedor e só então o cofre. Se o provedor recusar (ou não responder), nada é gravado e a chave que já valia continua valendo.
    /// </summary>
    public async Task<ModelConnection> ConnectAsync(string provider, string key, CancellationToken ct)
    {
        settings.Validate(provider, key);
        await ProbeAsync(ModelProviders.Find(provider) ?? throw new EngineException("MODEL_PROVIDER_DENIED", "O provedor não está disponível."), key, ct);

        var previous = settings.Status();
        settings.Save(provider, key);
        // Os modelos escolhidos eram os de outro provedor: trocar de provedor volta cada finalidade ao padrão do novo.
        if (previous.Configured && previous.Provider != provider) preferences?.Clear();
        checks?.Write(new ModelCheck("connected", Now, null));
        return Connection();
    }

    /// <summary>
    /// Escolhe o modelo de uma finalidade entre os do provedor da chave guardada; nulo volta ao padrão. Um modelo fora da lista
    /// do provedor é recusado: a lista é o que o motor sabe chamar, e o agente nunca escolhe.
    /// </summary>
    public ModelConnection SetPurposeModel(string purpose, string? model)
    {
        if (ModelPurposes.Find(purpose) is null) throw new EngineException("MODEL_PURPOSE_UNKNOWN", "A finalidade não existe.");
        var status = settings.Status();
        if (!status.Configured) throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhuma chave está configurada.");

        var provider = Active(status);
        if (model is not null && provider.Models.All(option => option.Id != model))
            throw new EngineException("MODEL_CHOICE_DENIED", "Esse modelo não está disponível neste provedor.");

        preferences?.Write(purpose, model is null || model == provider.DefaultModel ? null : model);
        return Connection();
    }

    /// <summary>O modelo de cada finalidade no provedor dado. Uma escolha que não pertence ao provedor (de um provedor anterior) vale como não escolhida.</summary>
    private List<ModelPurposeState> Purposes(ModelProvider provider)
    {
        var chosen = preferences?.Read() ?? new Dictionary<string, string>();
        return ModelPurposes.All.Select(purpose =>
        {
            var model = ModelFor(provider, purpose.Id, chosen);
            return new ModelPurposeState(purpose.Id, purpose.Label, purpose.Description, model, model == provider.DefaultModel);
        }).ToList();
    }

    private string ModelFor(ModelProvider provider, string purpose, IReadOnlyDictionary<string, string>? chosen = null)
    {
        chosen ??= preferences?.Read() ?? new Dictionary<string, string>();
        return chosen.TryGetValue(purpose, out var model) && provider.Models.Any(option => option.Id == model) ? model : provider.DefaultModel;
    }

    /// <summary>
    /// Confere a chave guardada com o provedor. Uma recusa não é exceção: fica registrada na conexão, que é o que a pessoa
    /// consulta. Sem chave, falha com <c>MODEL_NOT_CONFIGURED</c>.
    /// </summary>
    public async Task<ModelConnection> TestAsync(CancellationToken ct)
    {
        if (!settings.TryGetKey(out var key))
            throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhuma chave está configurada.");

        try
        {
            await ProbeAsync(Active(settings.Status()), key, ct);
            checks?.Write(new ModelCheck("connected", Now, null));
        }
        catch (EngineException ex) when (ex.Code is "MODEL_AUTH_FAILED" or "MODEL_RATE_LIMITED" or "MODEL_UNAVAILABLE" or "MODEL_TIMEOUT" or "MODEL_INVALID_RESPONSE")
        {
            checks?.Write(new ModelCheck("error", Now, new ModelCheckError(ex.Code, ex.Message, Now)));
        }

        return Connection();
    }

    /// <summary>O provedor da chave guardada. Uma chave de antes da escolha de provedor é da OpenAI.</summary>
    private static ModelProvider Active(ModelStatus status) => ModelProviders.Find(status.Provider) ?? ModelProviders.Default;

    private async Task ProbeAsync(ModelProvider provider, string key, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_timeout);
        try
        {
            await transport.SendAsync(provider.Endpoint, key, ProbePrompt, ProbeMaxTokens, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EngineException("MODEL_TIMEOUT", "O modelo não respondeu no prazo.");
        }
    }

    public async Task<ModelCompletion> CompleteAsync(string prompt, CancellationToken ct)
    {
        if (!settings.TryGetKey(out var key))
            return new ModelCompletion("", 0, "MODEL_NOT_CONFIGURED");

        var provider = Active(settings.Status());
        prompt = Redact(prompt, key).Trim();
        if (prompt.Length == 0)
            return new ModelCompletion("", 0, "MODEL_PROMPT_EMPTY");
        if (prompt.Length > 4000) prompt = prompt[..4000];

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_timeout);
        ModelTransportResult result;
        try
        {
            result = await transport.SendAsync(provider.Endpoint, key, prompt, MaxTokens, budget.Token);
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
    public async Task<ChatCompletion> ChatAsync(ChatRequest request, CancellationToken ct, string purpose = ModelPurposes.Workflow)
    {
        if (chat is null || !settings.TryGetKey(out var key))
            throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhum modelo está configurado.");

        var provider = Active(settings.Status());
        var model = ModelFor(provider, purpose);
        var messages = request.Messages.Select(m => m with { Content = Redact(m.Content, key) }).ToList();
        var bounded = request with { Messages = messages, MaxTokens = Math.Clamp(request.MaxTokens, 1, MaxChatTokens) };

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(ChatTimeout);
        ChatTransportResult result;
        try
        {
            result = await chat.SendChatAsync(provider.Endpoint, key, model, bounded, budget.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new EngineException("MODEL_TIMEOUT", "O modelo não respondeu no prazo.");
        }

        var text = Redact(result.Text, key);
        if (text.Length > MaxChatText) text = text[..MaxChatText];
        return new ChatCompletion(text, model, result.ModelVersion, Math.Max(0, result.InputTokens), Math.Max(0, result.OutputTokens), HashOf(messages));
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
