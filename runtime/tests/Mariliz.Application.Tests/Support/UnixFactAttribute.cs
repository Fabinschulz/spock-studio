namespace Mariliz.Application.Tests.Support;

// No Windows aparece como ignorado, nunca como aprovado: um verde que não testou nada é pior que um aviso.
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "Só roda no Linux ou no macOS: confere o modo de arquivo Unix.";
    }
}
