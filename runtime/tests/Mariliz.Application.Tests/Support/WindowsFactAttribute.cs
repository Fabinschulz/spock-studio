namespace Mariliz.Application.Tests.Support;

// Em outros sistemas aparece como ignorado, nunca como aprovado: um verde que não testou nada é pior que um aviso.
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Só roda no Windows: usa a DPAPI do sistema.";
    }
}
