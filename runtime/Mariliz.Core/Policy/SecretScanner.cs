using System.Text.RegularExpressions;

namespace Mariliz.Core.Policy;

/// <summary>
/// Procura formatos conhecidos de credenciais. Devolve só a regra e a linha: o valor encontrado
/// nunca sai desta classe, então não pode vazar para log, relatório ou resultado de Eval.
/// </summary>
public static class SecretScanner
{
    private static readonly (string Rule, Regex Pattern)[] Rules =
    [
        ("aws-access-key", new Regex(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled)),
        ("github-token", new Regex(@"\bgh[pousr]_[A-Za-z0-9]{30,}\b", RegexOptions.Compiled)),
        ("slack-token", new Regex(@"\bxox[abprs]-[A-Za-z0-9-]{10,}\b", RegexOptions.Compiled)),
        // Chaves de provedores comuns. O texto do projeto sai para o provedor de modelo e entra no índice: um formato que o
        // scanner não conhece passa direto. Os prefixos têm tamanho mínimo para não pegar palavras com hífen (task-..., risk-...).
        ("openai-key", new Regex(@"\bsk-(?:proj-|svcacct-|admin-)?[A-Za-z0-9_-]{32,}", RegexOptions.Compiled)),
        ("anthropic-key", new Regex(@"\bsk-ant-[A-Za-z0-9_-]{20,}", RegexOptions.Compiled)),
        ("google-api-key", new Regex(@"\bAIza[0-9A-Za-z_-]{35}\b", RegexOptions.Compiled)),
        ("stripe-key", new Regex(@"\b[rs]k_(?:live|test)_[0-9A-Za-z]{16,}", RegexOptions.Compiled)),
        ("npm-token", new Regex(@"\bnpm_[A-Za-z0-9]{36}\b", RegexOptions.Compiled)),
        ("private-key", new Regex(@"-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY", RegexOptions.Compiled)),
        ("jwt", new Regex(@"\beyJ[A-Za-z0-9_-]{8,}\.eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", RegexOptions.Compiled)),
        ("connection-string-password", new Regex(@"(?:Server|Data Source|Host)\s*=[^\r\n]*?(?:Password|Pwd)\s*=\s*[^;\s""']{3,}", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("url-credentials", new Regex(@"\b[a-z][a-z0-9+.-]*://[^/\s:@]+:[^/\s@]{3,}@[^/\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("assigned-secret", new Regex(@"(?:password|passwd|secret|api[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]\s*[""']?(?=[A-Za-z0-9/+_\-]{12,})(?=[^""'\s]*[A-Za-z])(?=[^""'\s]*\d)[^""'\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase))
    ];

    public static IReadOnlyList<SecretFinding> Scan(string content)
    {
        var findings = new List<SecretFinding>();
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (var (rule, pattern) in Rules)
            {
                if (pattern.IsMatch(lines[i])) findings.Add(new SecretFinding(rule, i + 1));
            }
        }
        return findings;
    }

    public static bool ContainsSecret(string content) => Scan(content).Count > 0;

    public const string RedactedLine = "[linha omitida: possível segredo]";

    /// <summary>Troca por um marcador cada linha com formato de credencial. Usado antes de mostrar conteúdo de arquivo.</summary>
    public static string RedactLines(string content)
    {
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            if (Rules.Any(r => r.Pattern.IsMatch(lines[i]))) lines[i] = RedactedLine;
        return string.Join('\n', lines);
    }
}
