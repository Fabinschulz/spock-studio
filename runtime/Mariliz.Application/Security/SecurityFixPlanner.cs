using Mariliz.Application.Execution;
using Mariliz.Application.Ports;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Security;

internal static class SecurityFixPlanner
{
    public const string GitIgnoreHeader = "# Arquivos sensíveis (Spock Studio)";

    public static (SecurityPlan Plan, IReadOnlyList<ApprovedWrite> Writes) Plan(string projectId, IProjectFiles files, IReadOnlyList<SecurityFinding> findings)
    {
        var prepared = new List<(ReviewedChange Change, ApprovedWrite Write)>();
        var covered = new List<string>();

        var unprotected = findings.Where(finding => finding.Kind == FindingKinds.UnprotectedFile).ToList();
        if (unprotected.Count > 0)
        {
            var entries = unprotected.Select(finding => SensitiveFiles.IgnoreEntry(finding.Path)).Distinct(StringComparer.Ordinal);
            prepared.Add(ReviewedChanges.Prepare(files, ManagedScope.GitIgnore, AppendIgnored(files.ReadText(ManagedScope.GitIgnore), entries)));
            covered.AddRange(unprotected.Select(finding => finding.Id));
        }

        foreach (var group in findings.Where(finding => finding.Kind == FindingKinds.AiPermission && finding.Fixable).GroupBy(finding => finding.Path))
        {
            if (AiConfigRules.FixClaudeSettings(files.ReadText(group.Key)) is not { } fixedSettings) continue;
            prepared.Add(ReviewedChanges.Prepare(files, group.Key, fixedSettings));
            covered.AddRange(group.Select(finding => finding.Id));
        }

        var changes = prepared.Select(item => item.Change).ToList();
        var plan = new SecurityPlan(ReviewedChanges.PlanId("sec-", changes), projectId, changes, covered, findings.Count - covered.Count);
        return (plan, prepared.Select(item => item.Write).ToList());
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
