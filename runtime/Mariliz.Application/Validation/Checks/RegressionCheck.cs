using Mariliz.Core.Evals;
using static Mariliz.Application.Validation.EvalResults;

namespace Mariliz.Application.Validation.Checks;

internal sealed class RegressionCheck : IEvalCheck
{
    public string Id => EvalIds.Regression;

    public Task<EvalResult> RunAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct) =>
        Task.FromResult(Run(spec, ctx, completed));

    private static EvalResult Run(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> current)
    {
        if (ctx.PreviousEvals.Count == 0) return NotRun(spec, "Não há execução anterior para comparar.");
        var regressions = new List<string>();
        foreach (var previous in ctx.PreviousEvals.Where(p => p.Status == nameof(EvalStatus.Passed)))
        {
            var now = current.FirstOrDefault(r => r.Id == previous.Id);
            if (now is not null && now.Status != EvalStatus.Passed) regressions.Add($"{previous.Id}: passava e agora está {now.Status}");
        }
        return regressions.Count == 0
            ? Pass(spec, [$"{ctx.PreviousEvals.Count(p => p.Status == nameof(EvalStatus.Passed))} Eval(s) aprovado(s) antes continuam aprovados"])
            : Fail(spec, $"{regressions.Count} regressão(ões) em relação à execução anterior.", regressions);
    }
}
