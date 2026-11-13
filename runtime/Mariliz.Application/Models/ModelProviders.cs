namespace Mariliz.Application.Models;

/// <summary>
/// Um provedor de modelo que o Spock Studio sabe chamar. O endereço é fixo e conhecido: o transporte só fala com os endereços
/// desta lista, então uma chave nunca é enviada para um destino que a pessoa ou um agente tenha escolhido.
/// </summary>
/// <param name="DefaultModel">O modelo das conversas dos agentes. O agente não o escolhe.</param>
public sealed record ModelProvider(string Id, string Name, Uri Endpoint, string DefaultModel);

/// <summary>Como a interface lista um provedor: sem o endereço, que não é assunto de tela.</summary>
public sealed record ModelProviderInfo(string Id, string Name, string Model);

/// <summary>Os provedores disponíveis nesta versão. É a única lista: validação de provedor, destino permitido e modelo saem daqui.</summary>
public static class ModelProviders
{
    public static readonly ModelProvider OpenAi = new("openai", "OpenAI", new Uri("https://api.openai.com/v1/chat/completions"), "gpt-4o-mini");

    public static readonly ModelProvider Anthropic = new("anthropic", "Anthropic", new Uri("https://api.anthropic.com/v1/messages"), "claude-haiku-5-5");

    public static IReadOnlyList<ModelProvider> All { get; } = [OpenAi, Anthropic];

    /// <summary>O provedor de quem já tinha uma chave antes de haver escolha: só havia a OpenAI.</summary>
    public static ModelProvider Default => OpenAi;

    public static ModelProvider? Find(string? id) => All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.Ordinal));

    /// <summary>O provedor dono de um endereço, ou nulo se o endereço não está na lista. Esquema, servidor e caminho precisam bater.</summary>
    public static ModelProvider? ByEndpoint(Uri endpoint) => All.FirstOrDefault(provider =>
        string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.Host, provider.Endpoint.Host, StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.AbsolutePath, provider.Endpoint.AbsolutePath, StringComparison.Ordinal));

    public static IReadOnlyList<ModelProviderInfo> Available() =>
        All.Select(provider => new ModelProviderInfo(provider.Id, provider.Name, provider.DefaultModel)).ToList();
}
