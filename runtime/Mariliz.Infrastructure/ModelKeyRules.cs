using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Models;

namespace Mariliz.Infrastructure;

/// <summary>O que vale para uma chave de provedor, em qualquer lugar onde ela seja guardada.</summary>
internal static partial class ModelKeyRules
{
    /// <summary>Recusa provedor fora da lista e chave fora do formato, sem nunca repetir a chave na mensagem.</summary>
    public static void Validate(string provider, string key)
    {
        if (ModelProviders.Find(provider) is null)
            throw new EngineException("MODEL_PROVIDER_DENIED", "O provedor não está disponível.");
        if (string.IsNullOrWhiteSpace(key) || !IsValidKey(key))
            throw new EngineException("MODEL_KEY_REJECTED", "A chave não tem o formato esperado.");
    }

    public static bool IsValidKey(string key) => KeyPattern().IsMatch(key);

    [GeneratedRegex("^[A-Za-z0-9_-]{20,200}$")]
    private static partial Regex KeyPattern();
}
