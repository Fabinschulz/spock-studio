using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;
using Mariliz.Core.Text;

namespace Mariliz.Application.Security;

internal sealed record PlannedFix(SecurityChange Change, string Content);

// O plano depende só do estado dos arquivos: refeito na hora de aplicar, ele tem de dar o mesmo id que a pessoa revisou.
internal static class SecurityFixPlanner
{
    public const string GitIgnoreHeader = "# Arquivos sensíveis (Spock Studio)";

    public static (SecurityPlan Plan, IReadOnlyList<PlannedFix> Fixes) Plan(string projectId, IProjectFiles files, IReadOnlyList<SecurityFinding> findings)
    {
        var fixes = new List<PlannedFix>();
        var covered = new List<string>();

        var unprotected = findings.Where(finding => finding.Kind == FindingKinds.UnprotectedFile).ToList();
        if (unprotected.Count > 0)
        {
            var before = files.ReadText(ManagedScope.GitIgnore);
            var entries = unprotected.Select(finding => SensitiveFiles.IgnoreEntry(finding.Path)).Distinct(StringComparer.Ordinal);
            fixes.Add(Fix(files, ManagedScope.GitIgnore, before, AppendIgnored(before, entries)));
            covered.AddRange(unprotected.Select(finding => finding.Id));
        }

        foreach (var group in findings.Where(finding => finding.Kind == FindingKinds.AiPermission && finding.Fixable).GroupBy(finding => finding.Path))
        {
            if (AiConfigRules.FixClaudeSettings(files.ReadText(group.Key)) is not { } fixedSettings) continue;
            fixes.Add(Fix(files, group.Key, files.ReadText(group.Key), fixedSettings));
            covered.AddRange(group.Select(finding => finding.Id));
        }

        var planId = "sec-" + Hashing.Sha256(string.Join("\n", fixes.Select(fix => $"{fix.Change.Path}|{fix.Change.BeforeHash}|{fix.Change.AfterHash}")))[..12];
        var plan = new SecurityPlan(planId, projectId, fixes.Select(fix => fix.Change).ToList(), covered, findings.Count - covered.Count);
        return (plan, fixes);
    }

    private static PlannedFix Fix(IProjectFiles files, string path, string? before, string after)
    {
        // A diferença vai para a tela: linha com formato de credencial sai dos dois lados, como no plano do Harness.
        var (diff, truncated) = UnifiedDiff.Create(path, before is null ? null : SecretScanner.RedactLines(before), SecretScanner.RedactLines(after));
        var beforeBytes = files.ReadBytes(path);
        var op = beforeBytes is null ? OpKinds.Create : OpKinds.Update;
        var change = new SecurityChange(path, op, diff, truncated, beforeBytes is null ? null : Hashing.Sha256(beforeBytes), Hashing.Sha256(after));
        return new PlannedFix(change, after);
    }

    private static string AppendIgnored(string? before, IEnumerable<string> entries)
    {
        var newline = before?.Contains("\r\n") == true ? "\r\n" : "\n";
        var text = before ?? "";
        if (text.Length > 0 && !text.EndsWith('\n')) text += newline;
        if (text.Length > 0) text += newline;
        return text + GitIgnoreHeader + newline + string.Join(newline, entries) + newline;
    }
}
