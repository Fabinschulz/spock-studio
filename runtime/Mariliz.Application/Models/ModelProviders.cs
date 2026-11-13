namespace Mariliz.Application.Models;

public sealed record ModelOption(string Id, string Name);

// O transporte só fala com os endereços desta lista, então uma chave nunca é enviada para um destino que a pessoa ou um agente tenha escolhido.
/// <param name="DefaultModel">O agente nunca o escolhe.</param>
public sealed record ModelProvider(string Id, string Name, Uri Endpoint, string DefaultModel, IReadOnlyList<ModelOption> Models);

// Sem o endereço, que não é assunto de tela.
public sealed record ModelProviderInfo(string Id, string Name, string Model, IReadOnlyList<ModelOption> Models);

// É a única lista: validação de provedor, destino permitido e modelo saem daqui.
public static class ModelProviders
{
    public static readonly ModelProvider OpenAi = new("openai", "OpenAI", new Uri("https://api.openai.com/v1/chat/completions"), "gpt-4o-mini",
        [new ModelOption("gpt-4o-mini", "GPT-4o mini"), new ModelOption("gpt-4o", "GPT-4o")]);

    public static readonly ModelProvider Anthropic = new("anthropic", "Anthropic", new Uri("https://api.anthropic.com/v1/messages"), "claude-haiku-5-5",
        [new ModelOption("claude-haiku-5-5", "Claude Haiku 5.5"), new ModelOption("claude-sonnet-5-5", "Claude Sonnet 5.5"), new ModelOption("claude-opus-5-5", "Claude Opus 5.5")]);

    public static IReadOnlyList<ModelProvider> All { get; } = [OpenAi, Anthropic];

    // Antes de haver escolha só havia a OpenAI.
    public static ModelProvider Default => OpenAi;

    public static ModelProvider? Find(string? id) => All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.Ordinal));

    // Esquema, servidor e caminho precisam bater.
    public static ModelProvider? ByEndpoint(Uri endpoint) => All.FirstOrDefault(provider =>
        string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.Host, provider.Endpoint.Host, StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.AbsolutePath, provider.Endpoint.AbsolutePath, StringComparison.Ordinal));

    public static IReadOnlyList<ModelProviderInfo> Available() =>
        All.Select(provider => new ModelProviderInfo(provider.Id, provider.Name, provider.DefaultModel, provider.Models)).ToList();
}
