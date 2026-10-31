using System.Text;
using Mariliz.Core.Discovery;
using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>Monta o conteúdo dos arquivos que a aplicação escreve a partir do que foi detectado.</summary>
internal static class RuleContent
{
    internal static DesiredFile Asset(string path, ComponentKind kind, string componentId, string asset, string reason) =>
        new(path, kind, componentId, DecisionOrigins.Baseline, reason, AssetLoader.Read(asset), "file");

    internal static string ForStack(string asset, List<StackFinding> stacks, string[] globs)
    {
        var paths = new StringBuilder();
        foreach (var stack in stacks.OrderBy(s => s.Root, StringComparer.Ordinal))
            foreach (var glob in globs)
                paths.Append("  - \"").Append(stack.Root.Length == 0 ? glob : stack.Root + "/" + glob).Append("\"\n");

        var detail = string.Join(", ", stacks.Select(s => $"{s.Name} {s.Version ?? "(version unknown)"} at {(s.Root.Length == 0 ? "." : s.Root)}"));
        return AssetLoader.Read(asset).Replace("{{paths}}", paths.ToString().TrimEnd('\n')).Replace("{{detail}}", detail);
    }

    internal static string ForRepository(IReadOnlyList<ToolingFinding> commands)
    {
        var sb = new StringBuilder();
        sb.Append("# Repository commands\n\n");
        sb.Append("Managed by Mariliz Studio. Detected from the repository's own files; each row cites its evidence.\n");
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
        sb.Append("Managed by Mariliz Studio. Proposed by the Harness Engineer and approved by a person.\n");
        sb.Append("Why: ").Append(rule.Justification).Append('\n');
        sb.Append("Evidence: ").Append(string.Join("; ", rule.Evidence)).Append("\n\n");
        sb.Append(rule.Content.Replace("\r\n", "\n").TrimEnd()).Append('\n');
        return sb.ToString();
    }
}
