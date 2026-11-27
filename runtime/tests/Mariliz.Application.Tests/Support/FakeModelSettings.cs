using Mariliz.Application.Models;

namespace Mariliz.Application.Tests.Support;

// Uma chave só na memória do teste; sem chave, o Boot não deve nem tentar o modelo.
internal sealed class FakeModelSettings(bool configured, string key = "sk-chave-de-teste-do-boot-0001") : IModelSettings
{
    public ModelStatus Status() => new(configured, configured ? "openai" : "");
    public void Save(string provider, string value) { }
    public void Clear() { }

    public bool TryGetKey(out string value)
    {
        value = configured ? key : "";
        return configured;
    }
}
