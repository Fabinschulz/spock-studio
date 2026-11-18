using Mariliz.Application.Mcp;
using Mariliz.Infrastructure;

namespace Mariliz.Runtime;

// O app aberto pelo ícone herda o PATH de antes da instalação: procurar só no PATH diria "sem lançador" logo depois de
// instalar. O instalador do Windows põe o lançador em bin\ ao lado do motor; no Linux os dois ficam na mesma pasta.
internal static class LauncherLocator
{
    public static bool IsInstalled(string? enginePath)
    {
        if (ExecutableLocator.FromEnvironment().Find(IdeConnection.Launcher) is not null) return true;
        if (Path.GetDirectoryName(enginePath) is not { } dir) return false;
        var name = OperatingSystem.IsWindows() ? IdeConnection.Launcher + ".exe" : IdeConnection.Launcher;
        return File.Exists(Path.Combine(dir, "bin", name)) || File.Exists(Path.Combine(dir, name));
    }
}
