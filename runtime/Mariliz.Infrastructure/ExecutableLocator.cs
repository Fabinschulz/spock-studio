using Mariliz.Application.Ports;

namespace Mariliz.Infrastructure;

public sealed class ExecutableLocator(IReadOnlyList<string> directories, IReadOnlyList<string> extensions) : IExecutableLocator
{
    // No Windows, `claude` sem extensão é o script de shell que o npm gera ao lado do .cmd, e ele não executa.
    private static readonly string[] WindowsExtensions = [".exe", ".cmd", ".bat"];

    public static ExecutableLocator FromEnvironment() =>
        new(SearchDirectories(), OperatingSystem.IsWindows() ? WindowsExtensions : [""]);

    public string? Find(string name)
    {
        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, name + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    // Um aplicativo aberto pelo ícone herda um PATH menor que o do terminal, então as pastas onde as CLIs costumam ficar entram também.
    private static List<string> SearchDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var fromPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim().Trim('"'));

        string[] usual =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, ".npm-global", "bin"),
            Path.Combine(home, ".bun", "bin"),
            Path.Combine(home, ".volta", "bin"),
            "/usr/local/bin",
            "/opt/homebrew/bin"
        ];

        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        return fromPath.Concat(usual).Where(directory => directory.Length > 0).Distinct(comparer).ToList();
    }
}
