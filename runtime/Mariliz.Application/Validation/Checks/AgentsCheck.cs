using Mariliz.Application.Discovery;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class AgentsCheck : IEvalCheck
{
    public string Id => EvalIds.Agent;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há agentes gerenciados a conferir.");
        var agents = ctx.OwnedFiles().Where(f => f.Path.StartsWith(".claude/agents/", StringComparison.Ordinal)).ToList();
        if (agents.Count == 0) return Pass(spec, ["Nenhum agente gerenciado"], ["Nenhum agente foi configurado neste projeto."]);

        var problems = new List<string>();
        foreach (var entry in agents)
        {
            var text = ctx.Files.ReadText(entry.Path);
            if (text is null) { problems.Add($"{entry.Path}: ilegível."); continue; }
            var name = Path.GetFileNameWithoutExtension(entry.Path);
            var issue = FrontmatterReader.Issue(text, name, requireTools: true);
            if (issue is not null) { problems.Add($"{entry.Path}: {issue}"); continue; }

            foreach (var section in AgentSections.Where(s => !text.Contains("\n" + s + "\n", StringComparison.Ordinal)))
                problems.Add($"{entry.Path}: falta a seção '{section}'.");

            var tools = FrontmatterReader.Read(text)!["tools"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (AgentTools.TryGetValue(entry.Path, out var allowed))
            {
                var extra = tools.Except(allowed, StringComparer.Ordinal).ToList();
                if (extra.Count > 0) problems.Add($"{entry.Path}: ferramentas fora do conjunto permitido ({string.Join(", ", extra)}).");
            }
            if (entry.Path == Catalog.PathCodeReviewer && tools.Any(t => WriteTools.Contains(t)))
                problems.Add($"{entry.Path}: agente somente leitura com ferramenta de escrita.");
        }

        return problems.Count == 0
            ? Pass(spec, agents.Select(a => $"{a.Path}: conforme").ToList())
            : Fail(spec, $"{problems.Count} não conformidade(s) nos agentes.", problems);
    }

    private static readonly Dictionary<string, string[]> AgentTools = new()
    {
        [Catalog.PathHarnessEngineer] = ["Read", "Grep", "Glob", "Write"],
        [Catalog.PathCodeReviewer] = ["Read", "Grep", "Glob", "Bash"],
        [Catalog.PathVerifier] = ["Read", "Grep", "Glob", "Bash", "Write"]
    };

    private static readonly string[] AgentSections = ["## Role", "## Use when", "## Do not use when", "## Required context", "## Inviolable rules", "## Output format"];
    private static readonly string[] WriteTools = ["Edit", "Write", "NotebookEdit", "MultiEdit"];
}
