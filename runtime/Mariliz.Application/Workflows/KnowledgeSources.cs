using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

/// <summary>
/// Fontes que podem ir para o índice. Segredo, binário, dependência, pasta de build, o que o <c>.gitignore</c> ignora e o
/// que <c>mariliz.yaml</c> exclui ficam de fora. Todo arquivo permitido vai com hash; o texto só vai quando mudou e cabe no
/// orçamento desta execução: o resto entra nas próximas, então o índice converge sem reenviar o projeto inteiro.
/// </summary>
internal static class KnowledgeSources
{
    public const int MaxFiles = 2_000;
    public const int MaxChars = 24_000;
    public const int MaxTextChars = 1_500_000;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg", ".exe", ".dll", ".pdb", ".so", ".dylib", ".zip", ".gz", ".tar",
        ".woff", ".woff2", ".ttf", ".pdf", ".bin", ".pem", ".key", ".p12", ".pfx", ".jks", ".keystore", ".map", ".lock", ".sqlite", ".db"
    };

    private static readonly HashSet<string> BlockedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".gitignore", "package-lock.json", "pnpm-lock.yaml", "yarn.lock", "packages.lock.json", "poetry.lock", "Cargo.lock", "composer.lock"
    };

    private static readonly IgnorePatterns BlockedPatterns = IgnorePatterns.Parse(
    [
        ".env", ".env.*", "id_rsa*", "id_ed25519*", "id_ecdsa*", "credentials.*", "secrets.*", "*.min.js", "*.min.css",
        "node_modules/", "bin/", "obj/", "dist/", "build/", "target/", ".git/", ".venv/", "venv/", "__pycache__/", ".next/"
    ]);

    public sealed record Source(string Path, string Hash, string? Text);

    public static IReadOnlyList<Source> Select(IProjectFiles files, IReadOnlyDictionary<string, string> indexed)
    {
        var ignored = IgnorePatterns.Parse((files.ReadText(".gitignore") ?? "").Split('\n'));
        var declared = DeclaredConfig.TryRead(files).Config?.Knowledge;
        var include = IgnorePatterns.Parse(declared?.Include ?? []);
        var exclude = IgnorePatterns.Parse(declared?.Exclude ?? []);

        var ranked = files.EnumerateFiles("")
            .Where(path => Allowed(path, ignored, include, exclude))
            .OrderBy(Rank)
            .ThenBy(path => path, StringComparer.Ordinal)
            .Take(MaxFiles)
            .ToList();

        var selected = new List<Source>();
        var budget = MaxTextChars;
        foreach (var path in ranked)
        {
            var text = files.ReadText(path);
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0')) continue;
            if (text.Length > MaxChars) text = text[..MaxChars];
            if (SecretScanner.ContainsSecret(text)) continue;
            var hash = Hashing.Sha256(text);
            var changed = !indexed.TryGetValue(path, out var previous) || previous != hash;
            var send = changed && text.Length <= budget;
            if (send) budget -= text.Length;
            selected.Add(new Source(path, hash, send ? text : null));
        }
        return selected;
    }

    private static bool Allowed(string path, IgnorePatterns ignored, IgnorePatterns include, IgnorePatterns exclude)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Split('/').Contains("..")) return false;
        var name = path.Split('/')[^1];
        if (BlockedNames.Contains(name) || BlockedExtensions.Contains(System.IO.Path.GetExtension(name))) return false;
        if (BlockedPatterns.Matches(path) || ignored.Matches(path) || exclude.Matches(path)) return false;
        return include.IsEmpty || include.Matches(path);
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
