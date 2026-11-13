namespace Mariliz.Application.Tests.Support;

/// <summary>
/// Teste que só faz sentido no Linux e no macOS (modo de arquivo Unix). No Windows aparece como <b>ignorado</b>, nunca como
/// aprovado: um verde que não testou nada é pior que um aviso.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute()
    {
        if (OperatingSystem.IsWindows()) Skip = "Só roda no Linux ou no macOS: confere o modo de arquivo Unix.";
    }
}
