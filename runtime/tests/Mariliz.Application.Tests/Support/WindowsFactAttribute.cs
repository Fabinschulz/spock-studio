namespace Mariliz.Application.Tests.Support;

/// <summary>
/// Teste que só faz sentido no Windows (a DPAPI real). Em outros sistemas aparece como <b>ignorado</b>, nunca como
/// aprovado: um verde que não testou nada é pior que um aviso.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Só roda no Windows: usa a DPAPI do sistema.";
    }
}
