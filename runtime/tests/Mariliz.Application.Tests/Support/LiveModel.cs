using Mariliz.Application.Models;

namespace Mariliz.Application.Tests.Support;

internal static class LiveKeys
{
    public const string OpenAi = "MARILIZ_LIVE_OPENAI_KEY";
    public const string Anthropic = "MARILIZ_LIVE_ANTHROPIC_KEY";
    public const string Google = "MARILIZ_LIVE_GOOGLE_KEY";

    public static string Read(string name) => Environment.GetEnvironmentVariable(name)!.Trim();
}

// A chave só existe na memória do teste: nada de cofre nem de arquivo.
internal sealed class EnvModelSettings(string provider, string key) : IModelSettings
{
    public ModelStatus Status() => new(true, provider);
    public void Save(string providerId, string value) { }
    public void Clear() { }

    public bool TryGetKey(out string value)
    {
        value = key;
        return true;
    }
}
