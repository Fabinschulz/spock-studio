namespace Mariliz.Application.Tests.Support;

/// <summary>
/// Teste que só faz sentido com o pacote do instalador montado. Fora da CI ele aparece como <b>ignorado</b>, nunca como aprovado:
/// um verde que não testou nada é pior que um aviso. Na CI do instalador a variável <see cref="RequiredVariable"/> o torna
/// obrigatório, e a ausência do pacote vira falha.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagedFactAttribute : FactAttribute
{
    public const string ResourcesVariable = "MARILIZ_PACKAGED_RESOURCES";
    public const string RequiredVariable = "MARILIZ_REQUIRE_PACKAGED";

    public PackagedFactAttribute()
    {
        if (IsRequired || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ResourcesVariable))) return;
        Skip = $"Sem o pacote do instalador: defina {ResourcesVariable} (veja scripts/package-intelligence.py bundle).";
    }

    public static bool IsRequired => Environment.GetEnvironmentVariable(RequiredVariable) == "1";
}

/// <summary>Como <see cref="PackagedFactAttribute"/>, para os testes que rodam o motor instalado (<c>MARILIZ_PACKAGED_ENGINE</c>).</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagedEngineFactAttribute : FactAttribute
{
    public const string EngineVariable = "MARILIZ_PACKAGED_ENGINE";

    public PackagedEngineFactAttribute()
    {
        if (PackagedFactAttribute.IsRequired || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EngineVariable))) return;
        Skip = $"Sem o motor do instalador: defina {EngineVariable} (veja scripts/smoke-installer.py).";
    }
}
