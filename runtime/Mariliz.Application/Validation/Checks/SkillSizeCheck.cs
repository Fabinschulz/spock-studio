using Mariliz.Application.Harness;
using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

// W3-2: uma skill de dezenas de milhares de tokens é anti-padrão (o catálogo registra uma assim numa referência). Vale para
// toda skill do projeto, não só as do Spock: a skill escrita à mão ocupa o mesmo contexto.
internal sealed class SkillSizeCheck : IEvalCheck
{
    private static readonly string[] Folders = [".claude/skills", ".agents/skills"];

    public string Id => EvalIds.SkillSize;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx)
    {
        var skills = Folders
            .SelectMany(folder => ctx.Files.EnumerateFiles(folder, 2))
            .Where(path => path.EndsWith("/SKILL.md", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => (Path: path, Length: ctx.Files.ReadText(path)?.Length ?? 0))
            .ToList();
        if (skills.Count == 0) return Pass(spec, ["Nenhuma skill no projeto"]);

        var tooLong = skills.Where(skill => skill.Length > StackSkillProfiles.MaxCharacters).ToList();
        return tooLong.Count == 0
            ? Pass(spec, skills.Select(skill => $"{skill.Path}: {skill.Length} caracteres").ToList())
            : Fail(spec, $"{tooLong.Count} skill(s) acima de {StackSkillProfiles.MaxCharacters} caracteres: divida por assunto ou leve o detalhe para a referência.",
                tooLong.Select(skill => $"{skill.Path}: {skill.Length} caracteres").ToList());
    }
}
