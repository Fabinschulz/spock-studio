using System.Text.Json;
using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Models;

// A chave não sai daqui para o runtime de IA, o journal ou a resposta. Uma conclusão do modelo não é veredito: quem decide continua sendo a verificação determinística.
public sealed class ModelGateway(
    IModelSettings settings,
    IModelTransport transport,
    TimeSpan? timeout = null,
    IModelChatTransport? chat = null,
    IModelCheckStore? checks = null,
    IClock? clock = null,
    IModelPreferences? preferences = null,
    IModelBudgetStore? budgets = null,
    TimeSpan? chatTimeout = null)
{
    public const int MaxChatTokens = 8192;
    public const int MaxChatText = 64000;

    private readonly TimeSpan _chatTimeout = chatTimeout ?? ModelTimeouts.Chat;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    // O provedor só precisa aceitá-la; a resposta não é usada.
    public const string ProbePrompt = "Responda apenas: ok";

    public const int ProbeMaxTokens = 8;

    private DateTimeOffset Now => clock?.UtcNow ?? DateTimeOffset.UtcNow;

    public ModelStatus Status() => settings.Status();

    public ModelConnection Connection()
    {
        var configured = settings.Providers();
        var fallback = Default(configured);
        var providers = ModelProviders.All.Select(provider => ProviderState(provider, configured.Contains(provider.Id), provider == fallback)).ToList();
        return new ModelConnection(configured.Count > 0, fallback?.Id ?? "", providers, fallback is null ? [] : Purposes(configured, fallback), Budgets());
    }

    // Finalidade sem teto próprio (o juiz gasta do fluxo) não abre execução sozinha.
    public int Budget(string purpose) =>
        ModelBudgets.Find(purpose) is { } budget ? ModelBudgets.Resolve(budget, budgets?.Read()) : throw UnknownBudget();

    // Não depende de chave: a revisão de contexto já mostra o teto antes de haver modelo.
    public ModelConnection SetBudget(string purpose, int? tokens)
    {
        var budget = ModelBudgets.Find(purpose) ?? throw UnknownBudget();
        if (tokens is { } value && !budget.Accepts(value))
            throw new EngineException("MODEL_BUDGET_OUT_OF_RANGE",
                $"O teto de {budget.Label} vai de {ModelBudgets.Format(budget.MinTokens)} a {ModelBudgets.Format(budget.MaxTokens)} tokens.");

        budgets?.Write(purpose, tokens == budget.DefaultTokens ? null : tokens);
        return Connection();
    }

    private static EngineException UnknownBudget() => new("MODEL_BUDGET_UNKNOWN", "Essa finalidade não tem teto de tokens próprio.");

    private List<ModelBudgetState> Budgets()
    {
        var chosen = budgets?.Read();
        return ModelBudgets.All.Select(budget =>
        {
            var tokens = ModelBudgets.Resolve(budget, chosen);
            return new ModelBudgetState(budget.Purpose, budget.Label, budget.Description, tokens, budget.DefaultTokens, budget.MinTokens, budget.MaxTokens,
                tokens == budget.DefaultTokens);
        }).ToList();
    }

    private ProviderConnection ProviderState(ModelProvider provider, bool configured, bool isDefault)
    {
        var check = configured ? checks?.Read(provider.Id) : null;
        var status = !configured ? "none" : check?.Status ?? "untested";
        return new ProviderConnection(provider.Id, provider.Name, configured, isDefault, status, configured ? settings.Protection(provider.Id) : null,
            check?.CheckedAt, check?.Error, provider.DefaultModel, provider.Models);
    }

    // Formato conferido, uma pergunta curta ao provedor e só então o cofre: se o provedor recusar (ou não responder), nada é gravado e a chave que já valia continua valendo.
    public async Task<ModelConnection> ConnectAsync(string provider, string key, CancellationToken ct)
    {
        settings.Validate(provider, key);
        await ProbeAsync(Find(provider), key, ct);

        settings.Save(provider, key);
        checks?.Write(provider, new ModelCheck("connected", Now, null));
        return Connection();
    }

    // As escolhas de modelo que eram dele deixam de valer, e o provedor padrão passa para outro que ainda tenha chave.
    public ModelConnection Remove(string provider)
    {
        Find(provider);
        settings.Remove(provider);
        checks?.Clear(provider);
        DropChoicesOf(provider);
        return Connection();
    }

    public ModelConnection SetDefaultProvider(string provider)
    {
        Find(provider);
        if (!settings.Providers().Contains(provider)) throw NotConfigured(provider);

        settings.SetDefault(provider);
        return Connection();
    }

    // Modelo de um provedor sem chave é recusado: a lista é o que o motor sabe chamar, e o agente nunca escolhe.
    public ModelConnection SetPurposeModel(string purpose, string? model)
    {
        if (ModelPurposes.Find(purpose) is null) throw new EngineException("MODEL_PURPOSE_UNKNOWN", "A finalidade não existe.");
        var configured = settings.Providers();
        if (configured.Count == 0) throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhuma chave está configurada.");

        if (model is not null && (ModelProviders.ByModel(model) is not { } owner || !configured.Contains(owner.Id)))
            throw new EngineException("MODEL_CHOICE_DENIED", "Esse modelo não está disponível: o provedor dele não tem chave.");

        // Escolher o próprio modelo padrão é voltar ao padrão: a finalidade segue o provedor padrão se ele mudar.
        preferences?.Write(purpose, model == Default(configured)?.DefaultModel ? null : model);
        return Connection();
    }

    // Uma recusa não é exceção: fica registrada na conexão, que é o que a pessoa consulta. Sem provedor, testa o padrão.
    public async Task<ModelConnection> TestAsync(string? provider, CancellationToken ct)
    {
        var target = provider is null ? Default(settings.Providers()) ?? throw NotConfigured(null) : Find(provider);
        if (!settings.TryGetKey(target.Id, out var key)) throw NotConfigured(target.Id);

        try
        {
            await ProbeAsync(target, key, ct);
            checks?.Write(target.Id, new ModelCheck("connected", Now, null));
        }
        catch (EngineException ex) when (ex.Code is "MODEL_AUTH_FAILED" or "MODEL_RATE_LIMITED" or "MODEL_UNAVAILABLE" or "MODEL_OVERLOADED" or "MODEL_TIMEOUT" or "MODEL_INVALID_RESPONSE")
        {
            checks?.Write(target.Id, new ModelCheck("error", Now, new ModelCheckError(ex.Code, ex.Message, Now)));
        }

        return Connection();
    }

    // Uma chave de antes da escolha de provedor é da OpenAI; sem o provedor guardado, vale o primeiro que tiver chave.
    private ModelProvider? Default(IReadOnlyList<string> configured)
    {
        if (configured.Count == 0) return null;
        var stored = settings.Status().Provider;
        var chosen = configured.Contains(stored) ? stored : configured[0];
        return ModelProviders.Find(chosen);
    }

    private static ModelProvider Find(string provider) =>
        ModelProviders.Find(provider) ?? throw new EngineException("MODEL_PROVIDER_DENIED", "O provedor não está disponível.");

    private static EngineException NotConfigured(string? provider) =>
        new("MODEL_NOT_CONFIGURED", provider is null ? "Nenhuma chave está configurada." : "Este provedor não tem chave configurada.");

    // Uma escolha vale se o provedor do modelo tem chave; senão a finalidade segue o padrão.
    private (ModelProvider Provider, string Model, bool Chosen)? Resolve(string purpose, IReadOnlyList<string> configured)
    {
        var chosen = preferences?.Read();
        if (chosen is not null && chosen.TryGetValue(purpose, out var model) && ModelProviders.ByModel(model) is { } owner && configured.Contains(owner.Id))
            return (owner, model, true);

        return Default(configured) is { } fallback ? (fallback, fallback.DefaultModel, false) : null;
    }

    private List<ModelPurposeState> Purposes(IReadOnlyList<string> configured, ModelProvider fallback) =>
        ModelPurposes.All.Select(purpose =>
        {
            var (provider, model, chosen) = Resolve(purpose.Id, configured) ?? (fallback, fallback.DefaultModel, false);
            return new ModelPurposeState(purpose.Id, purpose.Label, purpose.Description, provider.Id, model, !chosen);
        }).ToList();

    private void DropChoicesOf(string provider)
    {
        if (preferences is null) return;
        var dropped = preferences.Read().Where(choice => ModelProviders.ByModel(choice.Value)?.Id == provider).Select(choice => choice.Key).ToList();
        foreach (var purpose in dropped) preferences.Write(purpose, null);
    }

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

    // As mensagens saem sem a chave nem formato de segredo, e a resposta volta pela mesma redação. Quem cobra o orçamento é o chamador.
    public async Task<ChatCompletion> ChatAsync(ChatRequest request, CancellationToken ct, string purpose = ModelPurposes.Workflow)
    {
        var choice = Resolve(purpose, settings.Providers());
        if (chat is null || choice is null || !settings.TryGetKey(choice.Value.Provider.Id, out var key))
            throw new EngineException("MODEL_NOT_CONFIGURED", "Nenhum modelo está configurado.");

        var (provider, model, _) = choice.Value;
        var messages = request.Messages.Select(m => m with { Content = Redact(m.Content, key) }).ToList();
        var bounded = request with { Messages = messages, MaxTokens = Math.Clamp(request.MaxTokens, 1, MaxChatTokens) };

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(_chatTimeout);
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
