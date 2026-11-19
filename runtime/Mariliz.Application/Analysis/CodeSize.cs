using Mariliz.Core.Discovery;

namespace Mariliz.Application.Analysis;

// Arquivo grande é onde a mudança do agente mais erra e onde o review menos enxerga. Gerado não conta (ninguém o edita), e
// teste também não: um teste longo de cenários não é o risco que a lista quer mostrar.
internal static class CodeSize
{
    internal const int Threshold = 400;
    private const int MaxReported = 15;

    private static readonly string[] Extensions = [".cs", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".py", ".java", ".kt", ".go", ".rs", ".dart", ".vue", ".svelte"];
    private static readonly string[] GeneratedSuffixes = [".Designer.cs", ".g.cs", ".g.i.cs", ".generated.cs", "ModelSnapshot.cs", ".min.js", ".d.ts"];

    internal static List<ArchitectureFinding> LargeFiles(IReadOnlyList<string> all, Func<string, string?> read) =>
        all.Where(IsSource)
            .Select(path => (Path: path, Lines: read(path) is { } text ? Lines(text) : 0))
            .Where(file => file.Lines > Threshold)
            .OrderByDescending(file => file.Lines)
            .ThenBy(file => file.Path, StringComparer.Ordinal)
            .Take(MaxReported)
            .Select(file => new ArchitectureFinding(ArchitectureFindingCodes.LargeFile, ArchitectureSeverities.Info,
                $"{file.Path} tem {file.Lines} linhas", [new Evidence(file.Path)]))
            .ToList();

    private static bool IsSource(string path) =>
        Extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        && !GeneratedSuffixes.Any(suffix => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        && !path.Split('/').Contains("Migrations", StringComparer.Ordinal)
        && !IsTest(path);

    private static bool IsTest(string path)
    {
        var name = Path.GetFileName(path);
        return path.Split('/').SkipLast(1).Any(folder => folder is "tests" or "test" or "__tests__")
            || name.Contains(".test.", StringComparison.OrdinalIgnoreCase) || name.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Tests.cs", StringComparison.Ordinal) || name.StartsWith("test_", StringComparison.Ordinal);
    }

    private static int Lines(string text) => text.Length == 0 ? 0 : text.Count(c => c == '\n') + (text.EndsWith('\n') ? 0 : 1);
}
