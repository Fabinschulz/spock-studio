using System.Text.Json;
using Mariliz.Application.Ports;
using Mariliz.Core.Discovery;
using Mariliz.Core.Policy;

namespace Mariliz.Application.Discovery;

internal static class HarnessInventoryReader
{
    internal static HarnessInventory Read(IProjectFiles files, IReadOnlyList<string> all,
        HashSet<string> managed, List<DiscoveryWarning> warnings)
    {
        var agents = all.Where(p => p.StartsWith(".claude/agents/", StringComparison.Ordinal) && p.EndsWith(".md"))
            .Select(p => new InventoryItem(Path.GetFileNameWithoutExtension(p), p, managed.Contains(p), FrontmatterReader.Issue(files.ReadText(p), Path.GetFileNameWithoutExtension(p), requireTools: false))).ToList();

        var skills = all.Where(p => p.StartsWith(".claude/skills/", StringComparison.Ordinal) && p.EndsWith("/SKILL.md"))
            .Select(p => { var name = p.Split('/')[2]; return new InventoryItem(name, p, managed.Contains(p), FrontmatterReader.Issue(files.ReadText(p), name, requireTools: false)); }).ToList();

        var rules = all.Where(p => p.StartsWith(".claude/rules/", StringComparison.Ordinal) && p.EndsWith(".md"))
            .Select(p => new InventoryItem(Path.GetFileNameWithoutExtension(p), p, managed.Contains(p), null)).ToList();
        foreach (var f in new[] { "CLAUDE.md", ".claude/CLAUDE.md" }) if (all.Contains(f)) rules.Add(new InventoryItem(f, f, managed.Contains(f), null));

        var hooks = new List<InventoryItem>();
        var settings = files.ReadText(".claude/settings.json");
        if (settings is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(settings);
                if (doc.RootElement.TryGetProperty("hooks", out var h) && h.ValueKind == JsonValueKind.Object)
                {
                    foreach (var ev in h.EnumerateObject())
                    foreach (var group in ev.Value.EnumerateArray())
                    {
                        var matcher = group.TryGetProperty("matcher", out var m) ? m.GetString() : null;
                        var count = group.TryGetProperty("hooks", out var hs) ? hs.GetArrayLength() : 0;
                        hooks.Add(new InventoryItem($"{ev.Name}{(string.IsNullOrEmpty(matcher) ? "" : ":" + matcher)} ({count})", ".claude/settings.json", false, null));
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                hooks.Add(new InventoryItem("settings.json", ".claude/settings.json", false, "JSON inválido ou fora do formato esperado."));
                warnings.Add(new(DiscoveryWarningCodes.InvalidConfig, ".claude/settings.json não é um JSON válido; a aplicação não o alterará.", [".claude/settings.json"]));
            }
        }
        foreach (var script in all.Where(p => p.StartsWith(".claude/hooks/", StringComparison.Ordinal)))
            hooks.Add(new InventoryItem(Path.GetFileName(script), script, managed.Contains(script), null));

        var mcp = new List<InventoryItem>();
        var mcpText = files.ReadText(".mcp.json");
        if (mcpText is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(mcpText);
                if (doc.RootElement.TryGetProperty("mcpServers", out var servers) && servers.ValueKind == JsonValueKind.Object)
                    foreach (var s in servers.EnumerateObject())
                        mcp.Add(new InventoryItem(s.Name, ".mcp.json", false, SecretScanner.ContainsSecret(s.Value.GetRawText()) ? "Possível segredo literal na configuração; use variáveis de ambiente." : null));
            }
            catch (JsonException)
            {
                mcp.Add(new InventoryItem(".mcp.json", ".mcp.json", false, "JSON inválido."));
            }
        }

        var evals = all.Where(p => p.StartsWith(".claude/evals/", StringComparison.Ordinal))
            .Select(p => new InventoryItem(Path.GetFileName(p), p, managed.Contains(p), null)).ToList();

        var known = new[] { ".claude/agents/", ".claude/skills/", ".claude/rules/", ".claude/hooks/", ".claude/evals/", ".claude/worktrees/", ".claude/rag/", ".claude/mcp/" };
        var knownFiles = new[] { ".claude/CLAUDE.md", ".claude/settings.json", ".claude/settings.local.json", ".claude/launch.json" };
        var unknown = all.Where(p => p.StartsWith(".claude/", StringComparison.Ordinal) && !known.Any(k => p.StartsWith(k, StringComparison.Ordinal)) && !knownFiles.Contains(p)).ToList();
        if (unknown.Count > 0)
            warnings.Add(new(DiscoveryWarningCodes.UnrecognizedHarnessFiles, "Arquivos em .claude/ que a aplicação não reconhece; serão preservados.", unknown.Take(10).ToList()));

        return new HarnessInventory(agents, skills, hooks, rules, mcp, evals);
    }
}
