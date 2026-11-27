using System.Text.RegularExpressions;
using Mariliz.Application.Harness;
using Mariliz.Application.Quality.Git;
using Mariliz.Application.Security;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Quality;

/// <summary>O que um detector viu: onde, o trecho e como ele se liga à alteração. O texto da regra vem de <see cref="ReviewRules"/>.</summary>
internal sealed record FindingDraft(
    string RuleId,
    string Path,
    int? Line,
    string Excerpt,
    string Relation,
    string? Symbol,
    string Actual,
    string Detector,
    string? Severity = null,
    string? Confidence = null,
    string? ArchitectureRule = null);

/// <summary>
/// Os detectores determinísticos da revisão. Cada um olha só as linhas adicionadas (ou a lista de arquivos) e devolve um
/// rascunho com a evidência; nenhum chama modelo, roda comando do projeto nem lê fora do diff. Padrões que dependem do fluxo do
/// valor saem como hipótese, nunca como defeito.
/// </summary>
internal static partial class ChangeDetectors
{
    private const int MaxExcerpt = 160;

    public static IReadOnlyList<FindingDraft> Run(ChangeSet changes)
    {
        var drafts = new List<FindingDraft>();
        foreach (var path in changes.Conflicts)
            drafts.Add(new(ReviewRules.ConflictMarker, path, null, "Arquivo com conflito de merge não resolvido.", EvidenceRelations.ChangedFile, null,
                "O git marca este arquivo como não resolvido.", "git-unmerged"));

        foreach (var file in changes.Files)
        {
            SensitiveFile(file, drafts);
            HarnessFile(file, drafts);
            if (file.Status == FileStatuses.Deleted) { TestFileDeleted(file, drafts); continue; }
            if (!file.Analyzable) continue;
            Secrets(file, drafts);
            ConflictMarkers(file, drafts);
            CodePatterns(file, drafts);
            TestChanges(file, drafts);
        }
        ManifestWithoutLock(changes, drafts);
        return drafts;
    }

    // Os segredos primeiro e sem exceção de pasta: o mesmo detector que o resto do motor usa, mapeado de volta para a linha do diff.
    private static void Secrets(ChangedFile file, List<FindingDraft> drafts)
    {
        var added = file.AddedLines.ToList();
        if (added.Count == 0) return;
        // Uma linha com mais de um padrão vira um achado só, o do provedor quando há: o mesmo valor não é dois problemas.
        foreach (var onLine in SecretScanner.Scan(string.Join('\n', added.Select(line => line.Text))).GroupBy(secret => secret.Line))
        {
            if (onLine.Key < 1 || onLine.Key > added.Count) continue;
            var line = added[onLine.Key - 1];
            var chosen = onLine.FirstOrDefault(secret => ProviderRules.Contains(secret.Rule)) ?? onLine.First();
            // Em arquivo de teste a credencial costuma ser de exemplo: pede conferência, mas não é um problema confirmado nem bloqueia.
            // (Medido em 80 alterações reais: toda reprovação por segredo vinha de uma credencial de teste.)
            if (FileClassifier.IsTestPath(file.Path))
            {
                drafts.Add(new(ReviewRules.SecretGeneric, file.Path, line.NewNumber, SecretScanner.RedactedLine, EvidenceRelations.AddedLine, SymbolOf(file, line),
                    $"A linha adicionada, num arquivo de teste, casa com o padrão \"{chosen.Rule}\"; costuma ser uma credencial de exemplo. O valor não é mostrado.",
                    "secret-scanner", QualitySeverities.Medium));
                continue;
            }
            drafts.Add(new(ProviderRules.Contains(chosen.Rule) ? ReviewRules.SecretProvider : ReviewRules.SecretGeneric, file.Path, line.NewNumber, SecretScanner.RedactedLine,
                EvidenceRelations.AddedLine, SymbolOf(file, line), $"A linha adicionada casa com o padrão \"{chosen.Rule}\"; o valor não é mostrado.", "secret-scanner"));
        }
    }

    private static readonly HashSet<string> ProviderRules = new(StringComparer.Ordinal)
    {
        "aws-access-key", "github-token", "slack-token", "openai-key", "anthropic-key", "google-api-key", "stripe-key", "npm-token", "private-key"
    };

    private static void SensitiveFile(ChangedFile file, List<FindingDraft> drafts)
    {
        if (file.Status == FileStatuses.Deleted) return;
        var severity = SensitiveFiles.Severity(file.Path);
        if (severity is null) return;
        drafts.Add(new(ReviewRules.SensitiveFile, file.Path, null, file.Path, EvidenceRelations.ChangedFile, null,
            "O nome do arquivo é o de um que guarda credenciais ou chaves.", "sensitive-file",
            severity == FindingSeverities.High ? QualitySeverities.High : QualitySeverities.Medium));
    }

    private static void HarnessFile(ChangedFile file, List<FindingDraft> drafts)
    {
        if (Catalog.OwnerOf(file.Path) is not { } owner) return;
        drafts.Add(new(ReviewRules.HarnessFileChanged, file.Path, null, file.Path, EvidenceRelations.ChangedFile, null,
            $"O arquivo pertence ao componente do Harness {owner}.", "harness-scope"));
    }

    private static void ConflictMarkers(ChangedFile file, List<FindingDraft> drafts)
    {
        foreach (var line in file.AddedLines)
            if (ConflictMarker().IsMatch(line.Text))
                drafts.Add(new(ReviewRules.ConflictMarker, file.Path, line.NewNumber, Excerpt(line.Text), EvidenceRelations.AddedLine, SymbolOf(file, line),
                    "A linha adicionada é um marcador de conflito do git.", "conflict-marker"));
    }

    private static void CodePatterns(ChangedFile file, List<FindingDraft> drafts)
    {
        var language = Language.Of(file.Path);
        if (language == Language.Other) return;
        var test = FileClassifier.IsTestPath(file.Path);
        foreach (var line in file.AddedLines)
        {
            var text = line.Text;
            if (Language.IsComment(text)) continue;
            // Um padrão dentro de um texto entre aspas é dado (um teste que escreve código, uma expressão regular), não código que roda:
            // só o SQL concatenado precisa dos textos, porque o defeito é justamente juntar um texto a um valor.
            var code = WithoutStringContents(text);
            if (language == Language.CSharp && AsyncVoid().IsMatch(code) && !EventHandlerShape().IsMatch(code))
                drafts.Add(Draft(ReviewRules.AsyncVoid, file, line, "A linha declara um método async void que não tem a forma de um manipulador de evento.", "async-void"));
            if (EmptyCatch(language).IsMatch(code))
                drafts.Add(Draft(ReviewRules.EmptyCatch, file, line, "O bloco de tratamento da exceção não tem nenhuma instrução.", "empty-catch"));
            if (test) continue;
            if (TlsDisabled().IsMatch(code) || TlsDisabledByEnvironment().IsMatch(text))
                drafts.Add(Draft(ReviewRules.TlsDisabled, file, line, "A linha desliga a verificação do certificado do servidor.", "tls-verification"));
            if (SqlConcatenation().IsMatch(text))
                drafts.Add(Draft(ReviewRules.SqlConcatenation, file, line, "A linha monta um texto SQL juntando valores; não se sabe de onde eles vêm.", "sql-concatenation"));
            if (DynamicExecution(language).IsMatch(code))
                drafts.Add(Draft(ReviewRules.DynamicExecution, file, line, "A linha executa texto como código ou o insere como HTML; não se sabe de onde ele vem.", "dynamic-execution"));
        }
    }

    private static void TestChanges(ChangedFile file, List<FindingDraft> drafts)
    {
        if (!FileClassifier.IsTestPath(file.Path)) return;
        foreach (var line in file.AddedLines)
            if (TestSkipped().IsMatch(line.Text) && !Language.IsComment(line.Text))
                drafts.Add(Draft(ReviewRules.TestSkipped, file, line, "A linha adicionada desliga um teste.", "test-skipped"));

        var removed = file.RemovedLines.Count(line => TestCase().IsMatch(line.Text));
        var added = file.AddedLines.Count(line => TestCase().IsMatch(line.Text));
        if (removed > added)
            drafts.Add(new(ReviewRules.TestsRemoved, file.Path, null, $"{removed} casos de teste removidos, {added} adicionados.", EvidenceRelations.RemovedLines, null,
                $"O arquivo perdeu {removed - added} caso(s) de teste no saldo.", "test-balance"));
    }

    private static void TestFileDeleted(ChangedFile file, List<FindingDraft> drafts)
    {
        if (!FileClassifier.IsTestPath(file.Path)) return;
        var cases = file.RemovedLines.Count(line => TestCase().IsMatch(line.Text));
        drafts.Add(new(ReviewRules.TestFileDeleted, file.Path, null, file.Path, EvidenceRelations.ChangedFile, null,
            cases > 0 ? $"O arquivo foi apagado e tinha {cases} caso(s) de teste." : "O arquivo de teste foi apagado.", "test-file-deleted"));
    }

    private static void ManifestWithoutLock(ChangeSet changes, List<FindingDraft> drafts)
    {
        if (changes.Files.Any(file => FileClassifier.IsLockFile(file.Path))) return;
        foreach (var file in changes.Files.Where(file => FileClassifier.Name(file.Path) == "package.json" && file.Status != FileStatuses.Deleted && file.SkippedReason is null))
        {
            var line = file.AddedLines.FirstOrDefault(candidate => DependencyLine().IsMatch(candidate.Text));
            if (line is null) continue;
            drafts.Add(Draft(ReviewRules.ManifestWithoutLock, file, line, "O package.json mudou uma dependência e nenhum arquivo de bloqueio mudou junto.", "manifest-lock"));
        }
    }

    private static FindingDraft Draft(string ruleId, ChangedFile file, DiffLine line, string actual, string detector) =>
        new(ruleId, file.Path, line.NewNumber, Excerpt(line.Text), EvidenceRelations.AddedLine, SymbolOf(file, line), actual, detector);

    // O git só chuta a declaração mais próxima por uma regra de texto (às vezes "namespace X;" para uma linha dentro de um método).
    // O símbolo só vale quando parece a assinatura de um método ou função; do contrário é melhor não dizer nada.
    private static string? SymbolOf(ChangedFile file, DiffLine line) =>
        file.Hunks.FirstOrDefault(hunk => hunk.Lines.Contains(line))?.Symbol is { } symbol && symbol.Contains('(') ? symbol : null;

    // Esvazia o que está entre aspas (", ' e `) numa linha, com barra de escape; as aspas ficam. Um texto que não fecha na linha vale até o fim dela.
    internal static string WithoutStringContents(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        char? open = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (open is null)
            {
                builder.Append(c);
                if (c is '"' or '\'' or '`') open = c;
            }
            else if (c == '\\') i++;
            else if (c == open)
            {
                builder.Append(c);
                open = null;
            }
        }
        return builder.ToString();
    }

    // Nunca guarda a linha como veio: ela pode ter um valor que parece segredo sem que o detector tenha sido o de segredos.
    public static string Excerpt(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > MaxExcerpt) trimmed = trimmed[..MaxExcerpt] + "…";
        return SecretScanner.RedactLines(trimmed);
    }

    [GeneratedRegex(@"^(<<<<<<<|>>>>>>>)( |$)")]
    private static partial Regex ConflictMarker();

    [GeneratedRegex(@"\basync\s+void\s+\w+\s*[<(]")]
    private static partial Regex AsyncVoid();

    // Manipulador de evento: (object sender, XEventArgs e) ou nome On*/Handle*.
    [GeneratedRegex(@"\(\s*object\??\s+\w+\s*,\s*\w*EventArgs\??\s+\w+\s*\)|\basync\s+void\s+(On|Handle)[A-Z_]")]
    private static partial Regex EventHandlerShape();

    [GeneratedRegex(@"catch\s*(\([^)]*\))?\s*\{\s*\}")]
    private static partial Regex EmptyCatchBrace();

    [GeneratedRegex(@"except(\s+[\w.]+(\s+as\s+\w+)?)?\s*:\s*pass\b")]
    private static partial Regex EmptyExceptPython();

    private static Regex EmptyCatch(string language) => language == Language.Python ? EmptyExceptPython() : EmptyCatchBrace();

    [GeneratedRegex(@"ServerCertificateCustomValidationCallback\s*=\s*.*\btrue\b|DangerousAcceptAnyServerCertificateValidator|rejectUnauthorized\s*:\s*false|InsecureSkipVerify\s*:\s*true|verify\s*=\s*False", RegexOptions.IgnoreCase)]
    private static partial Regex TlsDisabled();

    // O valor vem entre aspas, então este padrão olha a linha como ela é.
    [GeneratedRegex(@"process\.env\.NODE_TLS_REJECT_UNAUTHORIZED\s*=\s*['""]?0")]
    private static partial Regex TlsDisabledByEnvironment();

    [GeneratedRegex(@"[""'`]\s*(SELECT\s.+\sFROM|INSERT\s+INTO|UPDATE\s+\w+\s+SET|DELETE\s+FROM)\b.*([""'`]\s*\+|\{\w|\$\{)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlConcatenation();

    [GeneratedRegex(@"\beval\s*\(|new\s+Function\s*\(|dangerouslySetInnerHTML|\.innerHTML\s*=")]
    private static partial Regex DynamicExecutionScript();

    [GeneratedRegex(@"\b(eval|exec)\s*\(")]
    private static partial Regex DynamicExecutionPython();

    private static Regex DynamicExecution(string language) => language == Language.Python ? DynamicExecutionPython() : DynamicExecutionScript();

    // Só o que desliga um teste de ponta a ponta. `t.skip(...)` e `t.Skip(...)` dentro do corpo do teste são a pulada de execução
    // (o ambiente não tem o recurso), e foram 7 falsos positivos em 80 alterações reais.
    [GeneratedRegex(@"Skip\s*=\s*""|\[Ignore\b|\b(it|test|describe|suite|context|specify)\.skip\b|\b(xit|xdescribe|xtest)\s*\(|@Ignore\b|@Disabled\b|pytest\.mark\.skip")]
    private static partial Regex TestSkipped();

    [GeneratedRegex(@"^\s*(\[(Fact|Theory|Test|TestMethod|TestCase)\b|(it|test)(\.each\([^)]*\))?\s*\(|(async\s+)?def\s+test_|@Test\b|func\s+Test\w+\()")]
    private static partial Regex TestCase();

    [GeneratedRegex(@"^\s*""(?!version""|name""|license""|description""|main""|type""|private"")[@\w./\-]+""\s*:\s*""[\^~><=*]*\d")]
    private static partial Regex DependencyLine();

    private static class Language
    {
        public const string CSharp = "csharp";
        public const string Script = "script";
        public const string Python = "python";
        public const string Other = "other";

        public static string Of(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => CSharp,
            ".js" or ".jsx" or ".ts" or ".tsx" or ".mjs" or ".cjs" or ".java" or ".go" or ".kt" or ".php" or ".rb" => Script,
            ".py" => Python,
            _ => Other
        };

        public static bool IsComment(string text)
        {
            var trimmed = text.TrimStart();
            return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('#') || trimmed.StartsWith('*') || trimmed.StartsWith("/*", StringComparison.Ordinal);
        }
    }
}
