using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Analysis;

// Somente leitura e sem modelo: o mesmo código dá o mesmo relatório. Ficam de fora o que o .gitignore ignora (dependências
// copiadas para dentro do repositório) e as pastas ocultas (.claude/worktrees duplicaria o projeto inteiro).
public static class ArchitectureAnalyzer
{
    private const int MaxDepth = 12;
    private const int MaxRead = 8_000;

    public static ArchitectureReport Analyze(string projectId, IProjectFiles files, DateTimeOffset analyzedAt)
    {
        var ignored = IgnorePatterns.Parse((files.ReadText(".gitignore") ?? "").Split('\n'));
        var all = files.EnumerateFiles("", MaxDepth).Where(path => !Hidden(path) && !ignored.Matches(path)).ToList();
        var read = Reader(files);
        var (projects, dotnet) = DotnetArchitecture.Analyze(files, all);
        var (frontends, frontend) = FrontendArchitecture.Analyze(all, read);
        var findings = dotnet.Concat(frontend).Concat(CodeSize.LargeFiles(all, read))
            .OrderBy(finding => finding.Severity == ArchitectureSeverities.Warning ? 0 : 1)
            .ToList();
        return new ArchitectureReport(projectId, analyzedAt, projects, frontends, findings, OwnImplementations.Detect(all, projects));
    }

    private static bool Hidden(string path) => path.Split('/').SkipLast(1).Any(folder => folder.StartsWith('.'));

    // O tamanho e os imports leem o mesmo arquivo; o teto protege um monorepo enorme.
    private static Func<string, string?> Reader(IProjectFiles files)
    {
        var cache = new Dictionary<string, string?>(StringComparer.Ordinal);
        return path =>
        {
            if (cache.TryGetValue(path, out var text)) return text;
            if (cache.Count >= MaxRead) return null;
            return cache[path] = files.ReadText(path);
        };
    }
}
