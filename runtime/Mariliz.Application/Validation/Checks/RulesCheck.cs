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
        if (ctx.Lock is null) return Block(spec, $"Sem {ctx.Naming.Lock}, não há regras gerenciadas a conferir.");
        if (!ctx.ConfiguresClaudeCode)
            return Pass(spec, ["Sem o Claude Code entre os ambientes"], ["As regras do Claude Code não se aplicam: nos outros ambientes o Spock grava só a ligação com a IDE e a skill do contrato."]);
        var rules = ctx.OwnedFiles().Where(f => f.Path.StartsWith(".claude/rules/", StringComparison.Ordinal)).ToList();
        var problems = new List<string>();

        if (!rules.Any(r => r.Path == ctx.Naming.RuleGeneral) && !(ctx.Declared?.Components.Exclude.Contains(Catalog.RuleGeneral) ?? false))
            problems.Add($"{ctx.Naming.RuleGeneral}: a regra geral não está no lock.");

        foreach (var rule in rules)
        {
            var text = ctx.Files.ReadText(rule.Path);
            if (string.IsNullOrWhiteSpace(text)) { problems.Add($"{rule.Path}: ausente ou vazio."); continue; }
            if (rule.Path.StartsWith(ctx.Naming.RulePrefix + "stack-", StringComparison.Ordinal))
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
