namespace Mariliz.Application.Models;

public sealed record ModelOption(string Id, string Name);

// O transporte só fala com os endereços desta lista, então uma chave nunca é enviada para um destino que a pessoa ou um agente tenha escolhido.
/// <param name="DefaultModel">O agente nunca o escolhe.</param>
public sealed record ModelProvider(string Id, string Name, Uri Endpoint, string DefaultModel, IReadOnlyList<ModelOption> Models);

// É a única lista: validação de provedor, destino permitido e modelo saem daqui.
public static class ModelProviders
{
    public const string DevGatewayVariable = "MARILIZ_DEV_MODEL_GATEWAY";

    // Só em build de desenvolvimento: um release nunca troca o destino, então a lista continua sendo a barreira da chave. Só o servidor
    // muda (https, sem credencial na URL); o caminho de cada provedor fica, e o Google nunca é redirecionado.
    private static Uri Gateway(string official)
    {
#if DEBUG
        var raw = Environment.GetEnvironmentVariable(DevGatewayVariable);
        if (Uri.TryCreate(raw, UriKind.Absolute, out var gateway) && gateway.Scheme == Uri.UriSchemeHttps && gateway.UserInfo.Length == 0)
            return new UriBuilder(official) { Host = gateway.Host, Port = gateway.IsDefaultPort ? -1 : gateway.Port }.Uri;
#endif
        return new Uri(official);
    }

    // Também só em Debug: um gateway pode não ter o modelo padrão, então MARILIZ_DEV_<PROVEDOR>_MODEL o troca e o põe na lista.
    private static string DevModel(string provider, string official)
    {
#if DEBUG
        var id = Environment.GetEnvironmentVariable($"MARILIZ_DEV_{provider}_MODEL");
        if (id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
            return id;
#endif
        return official;
    }

    private static IReadOnlyList<ModelOption> WithDefault(string defaultModel, params ModelOption[] official) =>
        official.Any(option => option.Id == defaultModel) ? official : [new ModelOption(defaultModel, defaultModel), .. official];

    public static readonly ModelProvider OpenAi = new("openai", "OpenAI", Gateway("https://api.openai.com/v1/chat/completions"), DevModel("OPENAI", "gpt-4o-mini"),
        WithDefault(DevModel("OPENAI", "gpt-4o-mini"), new ModelOption("gpt-4o-mini", "GPT-4o mini"), new ModelOption("gpt-4o", "GPT-4o")));

    public static readonly ModelProvider Anthropic = new("anthropic", "Anthropic", Gateway("https://api.anthropic.com/v1/messages"), DevModel("ANTHROPIC", "claude-haiku-5-5"),
        WithDefault(DevModel("ANTHROPIC", "claude-haiku-5-5"),
            new ModelOption("claude-haiku-5-5", "Claude Haiku 5.5"), new ModelOption("claude-sonnet-5-5", "Claude Sonnet 5.5"), new ModelOption("claude-opus-5-5", "Claude Opus 5.5")));

    // O modelo vai no endereço, então o que fica aqui é a base: o transporte completa com um modelo da lista.
    public static readonly ModelProvider Google = new("google", "Google Gemini", new Uri("https://generativelanguage.googleapis.com/v1beta/models"), "gemini-2.5-flash",
        [new ModelOption("gemini-2.5-flash", "Gemini 2.5 Flash"), new ModelOption("gemini-2.5-pro", "Gemini 2.5 Pro")]);

    public static IReadOnlyList<ModelProvider> All { get; } = [OpenAi, Anthropic, Google];

    // Antes de haver escolha só havia a OpenAI.
    public static ModelProvider Default => OpenAi;

    public static ModelProvider? Find(string? id) => All.FirstOrDefault(provider => string.Equals(provider.Id, id, StringComparison.Ordinal));

    // Esquema, servidor e caminho precisam bater.
    public static ModelProvider? ByEndpoint(Uri endpoint) => All.FirstOrDefault(provider =>
        string.Equals(endpoint.Scheme, "https", StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.Host, provider.Endpoint.Host, StringComparison.OrdinalIgnoreCase)
        && string.Equals(endpoint.AbsolutePath, provider.Endpoint.AbsolutePath, StringComparison.Ordinal));

    // O identificador de um modelo é de um provedor só: é por ele que se sabe com quem falar.
    public static ModelProvider? ByModel(string? model) => All.FirstOrDefault(provider => provider.Models.Any(option => option.Id == model));
}
