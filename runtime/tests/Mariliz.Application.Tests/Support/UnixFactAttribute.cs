namespace Mariliz.Application.Tests.Support;

// No Windows aparece como ignorado, nunca como aprovado: um verde que não testou nada é pior que um aviso.
[AttributeUsage(AttributeTargets.Method)]
public sealed class UnixFactAttribute : FactAttribute
{
    public UnixFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (OperatingSystem.IsWindows()) Skip = "Só roda no Linux ou no macOS: confere o modo de arquivo Unix.";
    }
}
