using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Security;

// Procura só no que pode ir para o repositório: o que o .gitignore já protege não é lido.
internal static class SecurityScanner
{
    public const int MaxFiles = 5_000;
    public const int MaxChars = 200_000;
    public const int MaxSecretsPerFile = 20;

    private static readonly IgnorePatterns Skipped = IgnorePatterns.Parse(
    [
        "node_modules/", "bin/", "obj/", "dist/", "build/", "target/", ".git/", ".venv/", "venv/", "__pycache__/", ".next/",
        "*.min.js", "*.min.css", "*.map"
    ]);

    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".svg", ".exe", ".dll", ".pdb", ".so", ".dylib", ".zip", ".gz", ".tar",
        ".woff", ".woff2", ".ttf", ".otf", ".pdf", ".bin", ".sqlite", ".db", ".mp3", ".mp4", ".wav"
    };

    private static readonly Dictionary<string, string> SecretLabels = new(StringComparer.Ordinal)
    {
        ["aws-access-key"] = "chave da AWS",
        ["github-token"] = "token do GitHub",
        ["slack-token"] = "token do Slack",
        ["openai-key"] = "chave da OpenAI",
        ["anthropic-key"] = "chave da Anthropic",
        ["google-api-key"] = "chave do Google",
        ["stripe-key"] = "chave do Stripe",
        ["npm-token"] = "token do npm",
        ["private-key"] = "chave privada",
        ["jwt"] = "token JWT",
        ["connection-string-password"] = "senha em string de conexão",
        ["url-credentials"] = "usuário e senha num endereço",
        ["assigned-secret"] = "senha ou chave atribuída no código"
    };

    public static (IReadOnlyList<SecurityFinding> Findings, int Scanned, bool Truncated) Scan(IProjectFiles files)
    {
        var ignored = IgnorePatterns.Parse((files.ReadText(ManagedScope.GitIgnore) ?? "").Split('\n'));
        var findings = new List<SecurityFinding>();
        var scanned = 0;
        var truncated = false;

        foreach (var path in files.EnumerateFiles(""))
        {
            if (Skipped.Matches(path)) continue;
            var gitignored = ignored.Matches(path);
            if (SensitiveFiles.Severity(path) is { } severity)
            {
                if (!gitignored) findings.Add(Unprotected(path, severity));
                continue;
            }
            if (gitignored || BinaryExtensions.Contains(Path.GetExtension(path))) continue;
            if (scanned >= MaxFiles)
            {
                truncated = true;
                break;
            }

            scanned++;
            var text = files.ReadText(path);
            if (string.IsNullOrEmpty(text) || text.Contains('\0')) continue;
            if (text.Length > MaxChars) text = text[..MaxChars];
            findings.AddRange(SecretScanner.Scan(text).Take(MaxSecretsPerFile).Select(found => Secret(path, found)));
        }

        findings.AddRange(AiConfigRules.Inspect(files).Select(item => AiPermission(item.Path, item.Rule)));
        var ordered = findings
            .OrderBy(finding => finding.Severity == FindingSeverities.High ? 0 : 1)
            .ThenBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Line)
            .ToList();
        return (ordered, scanned, truncated);
    }

    private static SecurityFinding Secret(string path, SecretFinding found) => Finding(
        FindingKinds.Secret, FindingSeverities.High, path, found.Line, found.Rule,
        $"Possível credencial: {SecretLabels.GetValueOrDefault(found.Rule, found.Rule)}",
        "Troque a credencial no serviço de origem, porque ela pode já ter sido copiada, e passe a ler o valor de uma variável de ambiente ou de um cofre. O Spock não mostra o valor nem altera código.",
        fixable: false);

    private static SecurityFinding Unprotected(string path, string severity) => Finding(
        FindingKinds.UnprotectedFile, severity, path, null, "unprotected-file",
        "Arquivo sensível fora do .gitignore",
        "Se ele for para o repositório, quem tiver acesso lê o conteúdo. A correção acrescenta o caminho ao .gitignore. Se o arquivo já foi enviado, tire-o do git com git rm --cached e troque as credenciais que ele guarda.",
        fixable: true);

    private static SecurityFinding AiPermission(string path, string rule) => rule switch
    {
        AiConfigRules.BypassPermissions => Finding(FindingKinds.AiPermission, FindingSeverities.High, path, null, rule,
            "O Claude Code está configurado para pular todas as confirmações",
            "Nesse modo ele grava arquivos e roda comandos sem perguntar. A correção remove o modo, e o Claude Code volta a pedir confirmação.",
            fixable: true),
        AiConfigRules.BroadBash => Finding(FindingKinds.AiPermission, FindingSeverities.Medium, path, null, rule,
            "O Claude Code pode rodar qualquer comando sem perguntar",
            "A permissão Bash sem restrição libera todo comando no terminal. A correção a remove; libere só o que precisa, como Bash(npm test).",
            fixable: true),
        AiConfigRules.CodexFullAccess => Finding(FindingKinds.AiPermission, FindingSeverities.High, path, null, rule,
            "O Codex roda sem sandbox",
            "Com danger-full-access ele pode ler e gravar fora do projeto. Troque sandbox_mode para \"workspace-write\" neste arquivo.",
            fixable: false),
        _ => Finding(FindingKinds.AiPermission, FindingSeverities.Medium, path, null, rule,
            "O Codex nunca pede aprovação",
            "Com approval_policy = \"never\" ele executa sem perguntar. Troque para \"on-request\" neste arquivo.",
            fixable: false)
    };

    private static SecurityFinding Finding(string kind, string severity, string path, int? line, string rule, string title, string advice, bool fixable) =>
        new(Hashing.Sha256($"{kind}|{rule}|{path}|{line}")[..12], kind, severity, path, line, rule, title, advice, fixable);
}
