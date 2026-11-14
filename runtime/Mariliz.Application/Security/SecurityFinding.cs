namespace Mariliz.Application.Security;

public static class FindingKinds
{
    public const string Secret = "secret";
    public const string UnprotectedFile = "unprotected-file";
    public const string AiPermission = "ai-permission";
}

public static class FindingSeverities
{
    public const string High = "high";
    public const string Medium = "medium";
}

/// <param name="Fixable">A correção cabe num plano do Spock (o .gitignore ou a configuração do Claude Code); o resto vira orientação.</param>
public sealed record SecurityFinding(
    string Id,
    string Kind,
    string Severity,
    string Path,
    int? Line,
    string Rule,
    string Title,
    string Advice,
    bool Fixable);

// Nunca leva o valor encontrado: só onde está e que tipo de credencial parece ser.
public sealed record SecurityReport(string ProjectId, DateTimeOffset ScannedAt, int FilesScanned, bool Truncated, IReadOnlyList<SecurityFinding> Findings);

public sealed record SecurityChange(string Path, string Op, string Diff, bool Truncated, string? BeforeHash, string AfterHash);

/// <param name="Manual">Achados que o plano não corrige: a pessoa resolve, seguindo a orientação de cada um.</param>
public sealed record SecurityPlan(string PlanId, string ProjectId, IReadOnlyList<SecurityChange> Changes, IReadOnlyList<string> FindingIds, int Manual);
