using System.Text.RegularExpressions;
using Mariliz.Application.Execution;
using Mariliz.Application.Harness;
using Mariliz.Application.Ports;
using Mariliz.Application.Specification;
using Mariliz.Application.Workspace;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Engineering;

// Só lê: os diários continuam sendo a base da reversão.
public sealed partial class HarnessRevisionService(WorkspaceAuthorizer workspace, IProjectFilesFactory files, IRunStore store)
{
    private const string OtherFiles = "other";

    public HarnessRevisions Read(string projectId)
    {
        if (!workspace.TryGetRoot(projectId, out var root)) throw EngineErrors.ProjectNotAuthorized();
        var project = files.Open(root);
        var lockFile = ManagedLockFile.Read(project);

        var journals = store.ListJournals(projectId);
        var byRunId = journals.ToDictionary(j => j.RunId, StringComparer.Ordinal);

        var revisions = journals
            .Where(j => j.Kind is RunKinds.Apply or RunKinds.Revert)
            .OrderBy(j => j.StartedAt)
            .Select(j => (Journal: j, Changes: Changes(j, byRunId)))
            .Where(item => item.Changes.Count > 0)
            .Select((item, index) => new HarnessRevision(
                index + 1,
                item.Journal.RunId,
                item.Journal.Kind,
                item.Journal.FinishedAt ?? item.Journal.StartedAt,
                item.Journal.State,
                item.Journal.RolledBack,
                item.Journal.RevertedByRunId,
                item.Journal.RevertsRunId,
                Group(item.Changes)))
            .Reverse()
            .ToList();

        var engine = lockFile?.Engine;
        return new HarnessRevisions(projectId, string.IsNullOrEmpty(engine) ? null : engine, BuildInfo.Version, revisions);
    }

    // A reversão marca as operações da aplicação como revertidas, então o efeito dela é o inverso dessas operações.
    private static List<(string Path, string Op)> Changes(RunJournal journal, IReadOnlyDictionary<string, RunJournal> byRunId)
    {
        if (journal.Kind == RunKinds.Apply)
            return journal.Ops
                .Where(op => op.Status != OpStatus.Pending && !HarnessNaming.IsLock(op.Path))
                .Select(op => (op.Path, op.Op))
                .ToList();

        if (journal.RevertsRunId is null || !byRunId.TryGetValue(journal.RevertsRunId, out var target) || target.Kind != RunKinds.Apply) return [];
        return target.Ops
            .Where(op => op.Status == OpStatus.Reverted && !HarnessNaming.IsLock(op.Path))
            .Select(op => (op.Path, Inverse(op.Op)))
            .ToList();
    }

    private static string Inverse(string op) => op switch
    {
        OpKinds.Create => OpKinds.Delete,
        OpKinds.Delete => OpKinds.Create,
        _ => op
    };

    private static List<HarnessRevisionComponent> Group(IEnumerable<(string Path, string Op)> changes) =>
        changes
            .GroupBy(change => ComponentOf(change.Path), StringComparer.Ordinal)
            .Select(group =>
            {
                List<string> Paths(string op) => group.Where(c => c.Op == op).Select(c => c.Path).Distinct().Order(StringComparer.Ordinal).ToList();
                var label = group.Key == OtherFiles ? "Outros arquivos" : HarnessLabels.Of(group.Key);
                return new HarnessRevisionComponent(group.Key, label, Paths(OpKinds.Create), Paths(OpKinds.Update), Paths(OpKinds.Delete));
            })
            .OrderBy(component => component.Id == OtherFiles)
            .ThenBy(component => component.Label, StringComparer.Ordinal)
            .ToList();

    private static string ComponentOf(string path)
    {
        if (Catalog.OwnerOf(path) is { } id) return id;
        if (path.StartsWith(SpecificationManifest.SkillDirectory + "/", StringComparison.Ordinal)) return Catalog.SpecificationStructure;
        if (CustomRulePath().Match(path) is { Success: true } match) return Catalog.CustomRulePrefix + match.Groups[1].Value;
        return OtherFiles;
    }

    [GeneratedRegex("^\\.claude/rules/(?:spock|mariliz)-repo-([a-z][a-z0-9-]{2,40})\\.md$")]
    private static partial Regex CustomRulePath();
}
