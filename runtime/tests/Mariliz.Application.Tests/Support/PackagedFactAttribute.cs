namespace Mariliz.Application.Tests.Support;

// Fora da CI aparece como ignorado, nunca como aprovado: um verde que não testou nada é pior que um aviso.
[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagedFactAttribute : FactAttribute
{
    public const string ResourcesVariable = "MARILIZ_PACKAGED_RESOURCES";
    public const string RequiredVariable = "MARILIZ_REQUIRE_PACKAGED";

    public PackagedFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (IsRequired || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ResourcesVariable))) return;
        Skip = $"Sem o pacote do instalador: defina {ResourcesVariable} (veja scripts/package-intelligence.py bundle).";
    }

    public static bool IsRequired => Environment.GetEnvironmentVariable(RequiredVariable) == "1";
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class PackagedEngineFactAttribute : FactAttribute
{
    public const string EngineVariable = "MARILIZ_PACKAGED_ENGINE";

    public PackagedEngineFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (PackagedFactAttribute.IsRequired || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EngineVariable))) return;
        Skip = $"Sem o motor do instalador: defina {EngineVariable} (veja scripts/smoke-installer.py).";
    }
}
