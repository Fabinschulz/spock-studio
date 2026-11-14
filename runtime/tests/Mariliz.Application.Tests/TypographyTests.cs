namespace Mariliz.Application.Tests;

// Pontuação comum (dois-pontos, vírgula, parênteses) diz o mesmo. A documentação em docs/ fica de fora, e só os arquivos versionados contam.
public class TypographyTests
{
    private static readonly string[] Extensions =
        [".cs", ".ts", ".tsx", ".scss", ".rs", ".py", ".json", ".md", ".html", ".svg", ".toml", ".yml", ".yaml", ".ps1"];

    [Fact]
    public void Nenhum_arquivo_de_codigo_ou_interface_tem_travessao()
    {
        var offenders = TrackedFiles()
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !path.StartsWith("docs/", StringComparison.Ordinal))
            .Select(path => Path.Combine(ContractFiles.Root, path))
            .Where(File.Exists)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, line, index))
                .Where(entry => entry.line.Contains('\u2014') || entry.line.Contains('\u2013'))
                .Select(entry => $"{Path.GetRelativePath(ContractFiles.Root, entry.path)}:{entry.index + 1}"))
            .ToList();

        Assert.True(offenders.Count == 0, "Travessão encontrado em: " + string.Join(", ", offenders.Take(20)));
    }

    private static IEnumerable<string> TrackedFiles()
    {
        var git = new System.Diagnostics.ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = ContractFiles.Root, RedirectStandardOutput = true, UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(git)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
}
