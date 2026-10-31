using Mariliz.Application.Discovery;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class SkillsCheck : IEvalCheck
{
    public string Id => EvalIds.Skill;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        if (ctx.Lock is null) return Block(spec, "Sem mariliz.lock, não há skills gerenciadas a conferir.");
        var skills = ctx.OwnedFiles().Where(f => f.Path.EndsWith("/SKILL.md", StringComparison.Ordinal)).ToList();
        if (skills.Count == 0) return Pass(spec, ["Nenhuma skill gerenciada"], ["Nenhuma skill foi configurada neste projeto."]);

        var problems = new List<string>();
        foreach (var entry in skills)
        {
            var folder = entry.Path.Split('/')[2];
            var issue = FrontmatterReader.Issue(ctx.Files.ReadText(entry.Path), folder, requireTools: false);
            if (issue is not null) problems.Add($"{entry.Path}: {issue}");
        }
        return problems.Count == 0
            ? Pass(spec, skills.Select(s => $"{s.Path}: conforme").ToList())
            : Fail(spec, $"{problems.Count} não conformidade(s) nas skills.", problems);
    }
}
