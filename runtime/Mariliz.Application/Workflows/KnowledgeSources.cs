using Mariliz.Application.Harness;
using Mariliz.Application.Integrations;
using Mariliz.Application.Management;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Workflows;

// O texto só vai quando mudou e cabe no orçamento desta execução: o resto entra nas próximas, então o índice converge sem reenviar o projeto inteiro. A tela de conhecimento usa a mesma avaliação, então o que ela mostra é o que o índice recebe.
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

    public enum Exclusion
    {
        None,
        Blocked,
        Gitignored,
        Excluded,
        NotIncluded,
        OverLimit,
        Empty,
        Secret
    }

    public sealed record Evaluated(string Path, Exclusion Reason, string? Text, string? Hash);

    public static (IReadOnlyList<string> Include, IReadOnlyList<string> Exclude) DeclaredRules(IProjectFiles files)
    {
        var declared = DeclaredConfig.TryRead(files).Config?.Knowledge;
        return (declared?.Include ?? [], declared?.Exclude ?? []);
    }

    public static IReadOnlyList<Source> Select(IProjectFiles files, IReadOnlyDictionary<string, string> indexed, IEnumerable<ExternalDocument>? external = null,
        IEnumerable<MemoryDocument>? memory = null)
    {
        var selected = new List<Source>();
        var budget = MaxTextChars;

        void Add(string path, string hash, string text)
        {
            var changed = !indexed.TryGetValue(path, out var previous) || previous != hash;
            var send = changed && text.Length <= budget;
            if (send) budget -= text.Length;
            selected.Add(new Source(path, hash, send ? text : null));
        }

        foreach (var file in Evaluate(files).Where(file => file.Reason == Exclusion.None)) Add(file.Path, file.Hash!, file.Text!);
        foreach (var document in external ?? []) Add(document.Path, document.Hash, document.Text);
        foreach (var document in memory ?? []) Add(document.Path, document.Hash, document.Text);
        return selected;
    }

    // Na ordem em que o índice recebe (documentação primeiro). Só lê o texto do que passou pelas regras de caminho.
    public static IEnumerable<Evaluated> Evaluate(IProjectFiles files)
    {
        var ignored = IgnorePatterns.Parse((files.ReadText(".gitignore") ?? "").Split('\n'));
        var (includeRules, excludeRules) = DeclaredRules(files);
        var include = IgnorePatterns.Parse(includeRules);
        var exclude = IgnorePatterns.Parse(excludeRules);

        var allowed = 0;
        foreach (var path in files.EnumerateFiles("").OrderBy(Rank).ThenBy(path => path, StringComparer.Ordinal))
        {
            var byPath = ByPath(path, ignored, include, exclude);
            if (byPath != Exclusion.None)
            {
                yield return new Evaluated(path, byPath, null, null);
                continue;
            }
            if (allowed >= MaxFiles)
            {
                yield return new Evaluated(path, Exclusion.OverLimit, null, null);
                continue;
            }
            allowed++;

            var text = files.ReadText(path);
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
            {
                yield return new Evaluated(path, Exclusion.Empty, null, null);
                continue;
            }
            if (text.Length > MaxChars) text = text[..MaxChars];
            if (SecretScanner.ContainsSecret(text))
            {
                yield return new Evaluated(path, Exclusion.Secret, null, null);
                continue;
            }
            yield return new Evaluated(path, Exclusion.None, text, Hashing.Sha256(text));
        }
    }

    private static Exclusion ByPath(string path, IgnorePatterns ignored, IgnorePatterns include, IgnorePatterns exclude)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Split('/').Contains("..")) return Exclusion.Blocked;
        // O prefixo das fontes externas é só do motor: um arquivo do projeto com esse nome não se passa por dado de terceiros.
        if (path.StartsWith(ExternalKnowledge.Prefix, StringComparison.Ordinal) || path.StartsWith(DecisionMemory.Prefix, StringComparison.Ordinal)) return Exclusion.Blocked;
        var name = path.Split('/')[^1];
        if (BlockedNames.Contains(name) || BlockedExtensions.Contains(System.IO.Path.GetExtension(name)) || BlockedPatterns.Matches(path))
            return Exclusion.Blocked;
        if (ignored.Matches(path)) return Exclusion.Gitignored;
        if (exclude.Matches(path)) return Exclusion.Excluded;
        return include.IsEmpty || include.Matches(path) ? Exclusion.None : Exclusion.NotIncluded;
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
