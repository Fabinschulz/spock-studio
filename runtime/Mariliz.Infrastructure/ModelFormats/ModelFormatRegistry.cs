using Mariliz.Application.Models;

namespace Mariliz.Infrastructure.ModelFormats;

internal static class ModelFormatRegistry
{
    private static readonly IReadOnlyDictionary<string, IModelFormat> ByProvider = new Dictionary<string, IModelFormat>(StringComparer.Ordinal)
    {
        [ModelProviders.OpenAi.Id] = new OpenAiFormat(),
        [ModelProviders.Anthropic.Id] = new AnthropicFormat(),
        [ModelProviders.Google.Id] = new GoogleFormat()
    };

    public static IModelFormat For(ModelProvider provider) => ByProvider[provider.Id];
}
