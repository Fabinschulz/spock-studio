using System.Text;
using System.Text.RegularExpressions;

namespace Mariliz.Core.Policy;

/// <summary>
/// Padrões no estilo do <c>.gitignore</c> sobre caminhos relativos com <c>/</c>: <c>*</c>, <c>**</c>, <c>?</c>, <c>[...]</c>,
/// <c>/</c> no início (ancora na raiz) e no fim (só pasta). Um padrão sem <c>/</c> vale em qualquer nível, e uma pasta
/// casada leva tudo o que está dentro dela. Negação (<c>!</c>) é ignorada de propósito: na dúvida, o arquivo fica de fora.
/// </summary>
public sealed class IgnorePatterns
{
    private readonly List<Regex> _rules = [];

    public static IgnorePatterns Parse(IEnumerable<string> lines)
    {
        var patterns = new IgnorePatterns();
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!') || line.Contains("..", StringComparison.Ordinal)) continue;
            line = line.Replace('\\', '/');
            var directoryOnly = line.EndsWith('/');
            line = line.Trim('/');
            if (line.Length == 0) continue;
            var anchored = raw.TrimStart().StartsWith('/') || line.Contains('/');
            patterns._rules.Add(new Regex(ToRegex(line, anchored, directoryOnly), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)));
        }
        return patterns;
    }

    public bool IsEmpty => _rules.Count == 0;

    /// <summary>Verdadeiro se o arquivo, ou alguma pasta acima dele, casa com um padrão.</summary>
    public bool Matches(string path)
    {
        foreach (var rule in _rules)
        {
            try
            {
                if (rule.IsMatch(path)) return true;
            }
            catch (RegexMatchTimeoutException)
            {
                return true;
            }
        }
        return false;
    }

    private static string ToRegex(string pattern, bool anchored, bool directoryOnly)
    {
        var body = new StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var slash = i + 2 < pattern.Length && pattern[i + 2] == '/';
                body.Append(slash ? "(?:.*/)?" : ".*");
                i += slash ? 2 : 1;
            }
            else if (c == '*') body.Append("[^/]*");
            else if (c == '?') body.Append("[^/]");
            else if (c == '[')
            {
                var end = pattern.IndexOf(']', i + 1);
                if (end < 0) { body.Append(@"\["); continue; }
                var set = pattern[(i + 1)..end].Replace(@"\", @"\\");
                body.Append('[').Append(set.StartsWith('!') ? "^" + set[1..] : set).Append(']');
                i = end;
            }
            else body.Append(Regex.Escape(c.ToString()));
        }

        var prefix = anchored ? "^" : "^(?:.*/)?";
        // Pasta casada: o caminho continua depois dela. Arquivo casado: termina nele (ou é uma pasta com coisas dentro).
        var suffix = directoryOnly ? "/.*$" : "(?:/.*)?$";
        return prefix + body + suffix;
    }
}
