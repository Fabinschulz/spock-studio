using System.Collections.Concurrent;
using Mariliz.Application.Models;
using Mariliz.Application.Ports;
using Microsoft.ML.Tokenizers;

namespace Mariliz.Infrastructure;

// Os modelos da OpenAI publicam o tokenizador; os outros provedores não, e o o200k_base serve de estimativa próxima.
public sealed class TiktokenTokenizers : ITokenizers
{
    private const string EstimateEncoding = "o200k_base";

    private readonly Lazy<Tokenizer> _estimate = new(() => TiktokenTokenizer.CreateForEncoding(EstimateEncoding));
    private readonly ConcurrentDictionary<string, Tokenizer?> _byModel = new(StringComparer.Ordinal);

    public TokenCounter For(string? provider, string? model)
    {
        if (provider == ModelProviders.OpenAi.Id && model is not null && _byModel.GetOrAdd(model, ForModel) is { } own)
            return new TokenCounter(text => own.CountTokens(text), true, $"Contagem exata com o tokenizador do {model}.");

        return new TokenCounter(text => _estimate.Value.CountTokens(text), false,
            "Estimativa: este provedor não publica o tokenizador, então a conta usa um parecido. A diferença costuma ser pequena.");
    }

    private static Tokenizer? ForModel(string model)
    {
        try
        {
            return TiktokenTokenizer.CreateForModel(model);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
