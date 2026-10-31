using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

/// <summary>Fontes que podem ir para o índice. Segredo, binário e pasta de build ficam de fora.</summary>
internal static class KnowledgeSources
{
    public const int MaxFiles = 40;
    public const int MaxChars = 24_000;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".exe", ".dll", ".pdb", ".zip", ".woff", ".woff2", ".pdf", ".bin", ".pem", ".key", ".p12", ".pfx"
    };

    public sealed record Source(string Path, string Hash, string? Text);

    public static IReadOnlyList<Source> Select(IProjectFiles files, IReadOnlyDictionary<string, string> indexed)
    {
        var ignored = Ignored(files);
        var ranked = files.EnumerateFiles("")
            .Where(path => Allowed(path, ignored))
            .OrderBy(Rank)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Take(MaxFiles)
            .ToList();

        var selected = new List<Source>();
        foreach (var path in ranked)
        {
            var text = files.ReadText(path);
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0')) continue;
            if (text.Length > MaxChars) text = text[..MaxChars];
            if (SecretScanner.ContainsSecret(text)) continue;
            var hash = Hashing.Sha256(text);
            var changed = !indexed.TryGetValue(path, out var previous) || previous != hash;
            selected.Add(new Source(path, hash, changed ? text : null));
        }
        return selected;
    }

    private static bool Allowed(string path, IReadOnlySet<string> ignored)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Split('/').Contains("..")) return false;
        if (Covers(ignored, path)) return false;
        var name = path.Split('/')[^1];
        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)) return false;
        var extension = System.IO.Path.GetExtension(name);
        return !BlockedExtensions.Contains(extension);
    }

    private static HashSet<string> Ignored(IProjectFiles files)
    {
        var ignored = new HashSet<string>(StringComparer.Ordinal);
        var text = files.ReadText(".gitignore");
        if (string.IsNullOrWhiteSpace(text)) return ignored;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!')) continue;
            if (line.IndexOfAny(['*', '?', '[']) >= 0) continue;
            var pattern = line.TrimStart('/');
            if (pattern.EndsWith('/')) pattern = pattern[..^1];
            if (pattern.Length == 0 || pattern.Contains("..", StringComparison.Ordinal)) continue;
            ignored.Add(pattern);
        }
        return ignored;
    }

    private static bool Covers(IReadOnlySet<string> ignored, string path)
    {
        foreach (var pattern in ignored)
        {
            if (path.Equals(pattern, StringComparison.Ordinal) || path.StartsWith(pattern + "/", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static int Rank(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".md" => 0,
            ".cs" => 1,
            ".ts" or ".tsx" or ".js" or ".jsx" or ".py" => 2,
            ".json" or ".yml" or ".yaml" => 3,
            _ => 4
        };
    }
}
