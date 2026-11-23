using System.Text;
using Mariliz.Application.Ideas;
using Mariliz.Application.Workflows;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Harness;

internal static class RuleContent
{
    internal static DesiredFile Asset(HarnessNaming naming, string path, ComponentKind kind, string componentId, string asset, string reason) =>
        new(path, kind, componentId, DecisionOrigins.Baseline, reason, naming.Render(AssetLoader.Read(asset)), "file");

    internal static string ForStack(HarnessNaming naming, string asset, List<StackFinding> stacks, string[] globs)
    {
        var paths = new StringBuilder();
        foreach (var stack in stacks.OrderBy(s => s.Root, StringComparer.Ordinal))
            foreach (var glob in globs)
                paths.Append("  - \"").Append(stack.Root.Length == 0 ? glob : stack.Root + "/" + glob).Append("\"\n");

        var detail = string.Join(", ", stacks.Select(s => $"{s.Name} {s.Version ?? "(version unknown)"} at {(s.Root.Length == 0 ? "." : s.Root)}"));
        return naming.Render(AssetLoader.Read(asset)).Replace("{{paths}}", paths.ToString().TrimEnd('\n')).Replace("{{detail}}", detail);
    }

    // Uma linha por padrão: a regra fica curta e o detalhe está a uma chamada da ferramenta MCP. A data da análise vai no texto,
    // então uma revisão do catálogo muda o hash e o plano mostra a regra como atualizada.
    internal static string ForPatterns(PatternRuleProfile profile, IReadOnlyList<StackFinding> stacks, IReadOnlyList<ReferencePattern> patterns,
        IReadOnlyList<string> notes)
    {
        var sb = new StringBuilder();
        if (profile.Globs.Length > 0)
        {
            sb.Append("---\npaths:\n");
            foreach (var glob in stacks.Select(stack => stack.Root).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                         .SelectMany(root => profile.Globs.Select(glob => root.Length == 0 ? glob : root + "/" + glob)))
                sb.Append("  - \"").Append(glob).Append("\"\n");
            sb.Append("---\n\n");
        }
        var first = patterns[0];
        sb.Append("# ").Append(profile.Name).Append(" patterns from the Organization\n\n");
        sb.Append("Managed by Spock Studio. Generated from the Organization catalog (").Append(first.Source).Append(", analysis of ")
          .Append(first.AnalyzedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append(").\n");
        sb.Append("These patterns come from other projects; they are not conventions of this one. Follow what this repository already\n");
        sb.Append("does, and reach for a pattern when the change calls for it, citing its id. Read a pattern in full (where it was seen,\n");
        sb.Append("when to use it, when to avoid it) with the `spock_architecture_reference` MCP tool, by id.\n\n");
        foreach (var pattern in patterns)
            sb.Append("- `").Append(pattern.Id).Append("` ").Append(pattern.Title).Append('\n');
        foreach (var note in notes)
            sb.Append('\n').Append(note).Append('\n');
        return sb.ToString();
    }

    internal static string ForArchitecture(IReadOnlyList<string> documents)
    {
        var sb = new StringBuilder();
        sb.Append("# Project architecture documents\n\n");
        sb.Append("Managed by Spock Studio. The person approved these documents step by step; they are the source of truth for\n");
        sb.Append("what to build. Read the ones that matter before you plan, implement or review a change.\n\n");
        foreach (var path in documents)
            sb.Append("- `").Append(path).Append("`: ").Append(DocumentPurpose(path)).Append('\n');
        sb.Append("\n## How to use them\n\n");
        sb.Append("- Cite the ids you work on (`REQ-..`, `CMP-..`, `US-..`) in plans, commits and reviews.\n");
        sb.Append("- A story is done when every acceptance criterion in the backlog is checked by a test or by a verification you ran.\n");
        sb.Append("- Do not add requirements, components or technologies the documents do not have. If the code needs a change in\n");
        sb.Append("  them, say so and ask; the person edits the documents, not the agent.\n");
        sb.Append("- When the code and a document disagree, report it as a finding instead of choosing one silently.\n");
        return sb.ToString();
    }

    private static string DocumentPurpose(string path) => path switch
    {
        _ when path.EndsWith(IdeaDocuments.SpecificationName, StringComparison.Ordinal) => "requirements (`REQ-..`, `NFR-..`), scope and open questions.",
        _ when path.EndsWith(IdeaDocuments.ArchitectureName, StringComparison.Ordinal) => "style, components (`CMP-..`) and the requirements each one serves, stack, data, risks and decisions.",
        _ => "epics and stories (`US-..`) with acceptance criteria, in the order they unblock each other."
    };

    internal static string ForRepository(IReadOnlyList<ToolingFinding> commands)
    {
        var sb = new StringBuilder();
        sb.Append("# Repository commands\n\n");
        sb.Append("Managed by Spock Studio. Detected from the repository's own files; each row cites its evidence.\n");
        sb.Append("Confirm a command before relying on it. If a command here is wrong, fix the source file it comes from.\n\n");
        sb.Append("| Purpose | Command | Evidence |\n| --- | --- | --- |\n");
        foreach (var t in commands.OrderBy(t => t.Kind, StringComparer.Ordinal).ThenBy(t => t.Command, StringComparer.Ordinal))
            sb.Append("| ").Append(t.Kind).Append(" | `").Append(t.Command).Append("` | ")
              .Append(string.Join("; ", t.Evidence.Select(e => e.Detail is null ? e.Path : $"{e.Path} ({e.Detail})"))).Append(" |\n");
        return sb.ToString();
    }

    internal static string ForProposal(ProposalRule rule)
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(rule.Title).Append("\n\n");
        sb.Append("Managed by Spock Studio. Proposed by the Harness Engineer and approved by a person.\n");
        sb.Append("Why: ").Append(rule.Justification).Append('\n');
        sb.Append("Evidence: ").Append(string.Join("; ", rule.Evidence)).Append("\n\n");
        sb.Append(rule.Content.Replace("\r\n", "\n").TrimEnd()).Append('\n');
        return sb.ToString();
    }
}
