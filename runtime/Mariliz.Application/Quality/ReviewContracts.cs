using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mariliz.Application.Quality;

public static class ReviewContract
{
    // Muda só com incompatibilidade: quem lê um registro antigo confere esta versão antes de interpretá-lo.
    public const string Version = "quality.review.v1";
}

public static class ReviewModes
{
    public const string WorkingTree = "working_tree";
    public const string Branch = "branch";

    public static bool IsKnown(string? mode) => mode is WorkingTree or Branch;
}

public static class FindingCategories
{
    public const string Bug = "bug";
    public const string Security = "security";
    public const string Architecture = "architecture";
    public const string Reliability = "reliability";
    public const string Performance = "performance";
    public const string Maintainability = "maintainability";
    public const string RequirementCompliance = "requirement-compliance";
    public const string Compatibility = "compatibility";

    public static readonly IReadOnlyList<string> All =
        [Bug, Security, Architecture, Reliability, Performance, Maintainability, RequirementCompliance, Compatibility];
}

public static class QualitySeverities
{
    public const string Critical = "critical";
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";
    public const string Informational = "informational";

    // Do mais grave para o menos grave.
    public static readonly IReadOnlyList<string> All = [Critical, High, Medium, Low, Informational];

    public static int Rank(string severity) => severity switch
    {
        Critical => 4,
        High => 3,
        Medium => 2,
        Low => 1,
        _ => 0
    };
}

/// <summary>
/// Quanto o próprio motor consegue provar. Não é uma porcentagem: cada nível tem um critério que se confere.
/// Confirmed: regra determinística, a evidência está nas linhas alteradas e o padrão sozinho já é o defeito.
/// Heuristic: regra determinística com evidência conferida, mas só uma pessoa sabe se o padrão é defeito naquele contexto.
/// Hypothesis: depende de contexto fora do diff; nunca é tratada como defeito confirmado nem bloqueia.
/// </summary>
public static class FindingConfidences
{
    public const string Confirmed = "confirmed";
    public const string Heuristic = "heuristic";
    public const string Hypothesis = "hypothesis";
}

public static class FindingStatuses
{
    public const string Open = "open";
}

// Como a evidência se liga à alteração.
public static class EvidenceRelations
{
    public const string AddedLine = "added-line";
    public const string RemovedLines = "removed-lines";
    public const string ChangedFile = "changed-file";
    public const string ProjectAnalysis = "project-analysis";
    public const string Requirement = "requirement";
    public const string GateOutput = "gate-output";
}

public static class ReviewVerdicts
{
    public const string Approved = "approved";
    public const string ApprovedWithWarnings = "approved_with_warnings";
    public const string Rejected = "rejected";
    public const string Inconclusive = "inconclusive";
    public const string NotRun = "not_run";
}

public static class FileStatuses
{
    public const string Added = "added";
    public const string Modified = "modified";
    public const string Deleted = "deleted";
    public const string Renamed = "renamed";
    public const string Untracked = "untracked";
    public const string Conflicted = "conflicted";
}

// Por que um arquivo alterado não foi analisado linha a linha.
public static class SkipReasons
{
    public const string Binary = "binary";
    public const string Generated = "generated";
    public const string ThirdParty = "third-party";
    public const string TooLarge = "too-large";
    public const string Conflict = "conflict";
    public const string Unreadable = "unreadable";
}

public sealed record FindingEvidence(string Excerpt, string Relation, string Origin, string? Symbol, IReadOnlyList<string> ContextConsulted);

/// <summary>
/// Um achado da revisão. A linha só existe quando a evidência está num arquivo alterado; o trecho nunca traz o valor de um segredo.
/// </summary>
public sealed record ReviewFinding(
    string Id,
    string RuleId,
    string ReviewId,
    string ProjectId,
    string Path,
    int? StartLine,
    int? EndLine,
    bool OnChangedLines,
    string Category,
    string Severity,
    string Confidence,
    string Title,
    string Description,
    FindingEvidence Evidence,
    string ExpectedBehavior,
    string ActualBehavior,
    string SuggestedCorrection,
    string? RelatedRequirement,
    string? RelatedArchitectureRule,
    string Source,
    string Detector,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ReviewFile(string Path, string? OldPath, string Status, int Added, int Removed, bool Analyzed, string? SkippedReason);

public sealed record JudgeResult(string Id, string Name, string Verdict, string Reason, IReadOnlyList<string> Criteria, IReadOnlyList<string> FindingIds, bool Deterministic);

public sealed record ReviewCounts(int Files, int FilesAnalyzed, int Findings, int Confirmed, int NeedsCheck, int Recommendations, IReadOnlyDictionary<string, int> BySeverity);

public sealed record ReviewRecord(
    string Contract,
    string Id,
    string ProjectId,
    string Mode,
    string? Branch,
    string? Base,
    string? Head,
    string? HeadCommit,
    DateTimeOffset CreatedAt,
    DateTimeOffset FinishedAt,
    string Verdict,
    string Summary,
    ReviewCounts Counts,
    IReadOnlyList<ReviewFile> Files,
    IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<JudgeResult> Judges,
    IReadOnlyList<string> RulesApplied,
    IReadOnlyList<string> ContextConsulted,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> NextActions,
    // Opcionais e nulos nas revisões antigas, que não tinham modelo, requisitos nem execução.
    IReadOnlyList<ModelRun>? ModelRuns = null,
    IReadOnlyList<RequirementCheck>? Requirements = null,
    IReadOnlyList<GateResult>? Gates = null);

/// <summary>
/// Uma chamada de modelo da revisão, como o motor a viu: qual modelo, o hash do que foi enviado (já sem segredos), o gasto e quantas
/// respostas valeram. O modelo só propõe; o que ele devolve sem evidência no diff é descartado e contado aqui.
/// </summary>
public sealed record ModelRun(string Purpose, string State, string? Model, string? ModelVersion, string? PromptHash, int InputTokens, int OutputTokens, int Accepted, int Discarded, string? FailureCode);

public static class ModelRunStates
{
    public const string Used = "used";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

/// <param name="Status"><c>met</c> (só com linha do diff como evidência), <c>partial</c>, <c>not_met</c> ou <c>unknown</c>.</param>
public sealed record RequirementCheck(int Index, string Text, string Status, string? Path, int? Line, string? Excerpt, string? Note);

public static class RequirementStatuses
{
    public const string Met = "met";
    public const string Partial = "partial";
    public const string NotMet = "not_met";
    public const string Unknown = "unknown";

    public static bool IsKnown(string? value) => value is Met or Partial or NotMet or Unknown;
}

/// <summary>Uma verificação do projeto (build, teste, lint) que a pessoa autorizou rodar.</summary>
public sealed record GateResult(
    string Id,
    string Kind,
    string Tool,
    string Command,
    string Directory,
    string Origin,
    string State,
    int? ExitCode,
    long DurationMs,
    string Summary,
    string? LogTail);

public static class GateStates
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
    public const string NotRun = "not_run";
    public const string TimedOut = "timed_out";
    public const string Cancelled = "cancelled";
}

public static partial class ReviewIds
{
    public const string Prefix = "rev-";

    public static string New() => Prefix + Guid.NewGuid().ToString("N")[..12];

    // O id vira nome de arquivo: só o formato exato passa.
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    // O mesmo defeito no mesmo lugar tem o mesmo id entre revisões: é o que permite comparar uma revisão com a anterior.
    public static string FindingId(string ruleId, string path, int? line, string excerpt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{ruleId}\n{path}\n{line}\n{excerpt}"));
        return "f-" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    [GeneratedRegex("^rev-[0-9a-f]{12}$")]
    private static partial Regex Pattern();
}

public interface IReviewStore
{
    void Save(ReviewRecord record);

    ReviewRecord? Find(string projectId, string reviewId);

    // Do mais novo para o mais antigo.
    IReadOnlyList<ReviewRecord> List(string projectId);

    bool Delete(string projectId, string reviewId);

    // As decisões da pessoa ficam à parte do relatório do motor, que não muda depois de gravado.
    DecisionLog LoadDecisions(string projectId);

    void SaveDecisions(string projectId, DecisionLog log);
}
