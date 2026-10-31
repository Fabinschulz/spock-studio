using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;

namespace Mariliz.Application.Discovery;

internal static class AiEnvironmentDetector
{
    internal static List<AiEnvironmentFinding> Detect(IProjectFiles files, IReadOnlyList<string> all, List<DiscoveryWarning> warnings)
    {
        var result = new List<AiEnvironmentFinding>();

        var claude = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".claude/", StringComparison.Ordinal))) claude.Add(new Evidence(".claude/"));
        foreach (var f in new[] { "CLAUDE.md", ".claude/CLAUDE.md", ".mcp.json" }) if (all.Contains(f)) claude.Add(new Evidence(f));
        if (claude.Count > 0)
        {
            var strong = claude.Any(e => e.Path == ".claude/");
            result.Add(new AiEnvironmentFinding(AiEnvironments.ClaudeCode, "Claude Code", true, strong ? Confidence.High : Confidence.Medium, claude));
        }

        var cursor = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".cursor/", StringComparison.Ordinal))) cursor.Add(new Evidence(".cursor/"));
        if (all.Contains(".cursorrules")) cursor.Add(new Evidence(".cursorrules"));
        if (cursor.Count > 0) result.Add(new AiEnvironmentFinding(AiEnvironments.Cursor, "Cursor", false, Confidence.High, cursor));

        var codex = new List<Evidence>();
        if (all.Any(p => p.StartsWith(".codex/", StringComparison.Ordinal))) codex.Add(new Evidence(".codex/"));
        if (all.Contains("AGENTS.md")) codex.Add(new Evidence("AGENTS.md", "lido por várias ferramentas; evidência fraca"));
        if (codex.Count > 0)
            result.Add(new AiEnvironmentFinding(AiEnvironments.Codex, "OpenAI Codex", false, codex.Any(e => e.Path == ".codex/") ? Confidence.High : Confidence.Low, codex));

        if (result.Count == 0)
            warnings.Add(new(DiscoveryWarningCodes.NoAiEnvironment, "Nenhum ambiente de IA foi detectado; selecione o que o projeto usa.", []));
        else if (result.Count > 1)
            warnings.Add(new(DiscoveryWarningCodes.MultipleAiEnvironments, $"Mais de um ambiente de IA detectado: {string.Join(", ", result.Select(r => r.Name))}.", []));
        return result;
    }
}
