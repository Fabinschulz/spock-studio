namespace Mariliz.Infrastructure;

/// <summary>
/// Onde o instalador coloca o interpretador. Medido em 2026-10-31: o sidecar autocontido tem 70,8 MB,
/// os scripts têm 34 KB e o zip oficial <c>python-3.12.10-embed-amd64.zip</c> tem 10,6 MB.
/// O pacote usa esse CPython embarcável no Windows e o python-build-standalone podado no macOS (≤ 30 MB) e no Linux (≤ 55 MB).
/// As dependências da inteligência ficam em <c>intelligence/packages</c>, fora do interpretador (ADR-0001).
/// Um interpretador maior que o orçamento é uma instalação completa, não o runtime do produto.
/// </summary>
public static class PythonRuntimeLayout
{
    public const long PackagedInterpreterBudgetBytes = 25L * 1024 * 1024;

    public static IEnumerable<string> Candidates(string baseDirectory, string? overridePath, string? packagedPath, string? pathVariable)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            yield return overridePath;
            yield break;
        }

        if (IsFile(packagedPath))
        {
            yield return packagedPath!;
            yield break;
        }

        var bundled = Path.Combine(baseDirectory, "python", OperatingSystem.IsWindows() ? "python.exe" : Path.Combine("bin", "python3"));
        if (File.Exists(bundled))
        {
            yield return bundled;
            yield break;
        }

        foreach (var found in SearchPath(pathVariable))
            yield return found;
    }

    private static bool IsFile(string? path)
    {
        try { return !string.IsNullOrWhiteSpace(path) && File.Exists(path); }
        catch (ArgumentException) { return false; }
        catch (IOException) { return false; }
    }

    private static IEnumerable<string> SearchPath(string? pathVariable)
    {
        var names = OperatingSystem.IsWindows() ? new[] { "py", "python3", "python" } : new[] { "python3", "python" };
        var extensions = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", "" } : new[] { "" };
        var dirs = (pathVariable ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            string? found = null;
            foreach (var dir in dirs)
            {
                foreach (var ext in extensions)
                {
                    string candidate;
                    try { candidate = Path.Combine(dir.Trim().Trim('"'), name + ext); }
                    catch (ArgumentException) { continue; }
                    if (!File.Exists(candidate)) continue;
                    found = candidate;
                    break;
                }
                if (found is not null) break;
            }
            if (found is not null && seen.Add(found)) yield return found;
        }
    }
}
