using System.Text.RegularExpressions;
using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class RulesCheck : IEvalCheck
{
    public string Id => EvalIds.Rule;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há regras gerenciadas a conferir.");
        var rules = ctx.OwnedFiles().Where(f => f.Path.StartsWith(".claude/rules/", StringComparison.Ordinal)).ToList();
        var problems = new List<string>();

        if (!rules.Any(r => r.Path == Catalog.PathRuleGeneral) && !(ctx.Declared?.Components.Exclude.Contains(Catalog.RuleGeneral) ?? false))
            problems.Add($"{Catalog.PathRuleGeneral}: a regra geral não está no lock.");

        foreach (var rule in rules)
        {
            var text = ctx.Files.ReadText(rule.Path);
            if (string.IsNullOrWhiteSpace(text)) { problems.Add($"{rule.Path}: ausente ou vazio."); continue; }
            if (rule.Path.Contains("mariliz-stack-", StringComparison.Ordinal))
            {
                var m = Regex.Match(text, @"\A---\n(?<fm>[\s\S]*?)\n---\n");
                if (!m.Success || !Regex.IsMatch(m.Groups["fm"].Value, @"^paths:\s*\n(\s+- "".+""\n?)+", RegexOptions.Multiline))
                    problems.Add($"{rule.Path}: frontmatter 'paths' ausente ou inválido.");
            }
        }
        return problems.Count == 0
            ? Pass(spec, rules.Select(r => $"{r.Path}: presente").ToList())
            : Fail(spec, $"{problems.Count} problema(s) nas regras.", problems);
    }
}
