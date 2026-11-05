using Mariliz.Application.Validation.Checks;
using Mariliz.Core.Evals;

namespace Mariliz.Application.Validation;

/// <summary>
/// Roda os Evals do catálogo, na ordem. Verificações determinísticas decidem tudo o que podem; evidências citam
/// caminhos, códigos e contagens, nunca valores de segredos.
/// </summary>
public static class EvalRunner
{
    private static readonly IReadOnlyDictionary<string, IEvalCheck> Checks = new IEvalCheck[]
    {
        new ConfigCheck(), new LockIntegrityCheck(), new AgentsCheck(), new SkillsCheck(), new HookCheck(),
        new RulesCheck(), new ReferencesCheck(), new SpecificationCheck(), new SecretsCheck(), new EnvironmentCheck(),
        new RequirementsCheck(), new IdempotenceCheck(), new RegressionCheck(), new ModelJudgeCheck()
    }.ToDictionary(check => check.Id, StringComparer.Ordinal);

    public static async Task<ValidationReport> RunAsync(ValidationContext ctx, CancellationToken ct, IEvalProgress? progress = null)
    {
        var requiredByDeclaration = ctx.Declared?.Evals.Required.ToHashSet(StringComparer.Ordinal) ?? [];
        var results = new List<EvalResult>();

        foreach (var spec in EvalCatalog.Specs)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Started(spec.Id, spec.Name, Wire(spec.Kind));
            var result = await RunCheckAsync(spec, ctx, results, ct);
            results.Add(result with { Required = spec.Required || requiredByDeclaration.Contains(spec.Id) });
            progress?.Completed(spec.Id, Wire(result.Status));
        }

        return new ValidationReport(results);
    }

    // Os mesmos valores de `kind` e `status` do resultado (`EvalResult`), em minúsculas e com `_`.
    private static string Wire(EvalKind kind) => kind == EvalKind.Model ? "model" : "deterministic";

    private static string Wire(EvalStatus status) => status switch
    {
        EvalStatus.Passed => "passed",
        EvalStatus.Failed => "failed",
        EvalStatus.Blocked => "blocked",
        _ => "not_run"
    };

    private static async Task<EvalResult> RunCheckAsync(EvalSpec spec, ValidationContext ctx, IReadOnlyList<EvalResult> completed, CancellationToken ct)
    {
        if (!Checks.TryGetValue(spec.Id, out var check))
            return EvalResults.NotRun(spec, "Eval desconhecido.");

        try
        {
            return await check.RunAsync(spec, ctx, completed, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Um Eval que quebra não vira aprovação: fica bloqueado, com o tipo do erro (sem o texto, que pode ter dados).
            return EvalResults.Block(spec, $"O Eval não pôde ser executado ({ex.GetType().Name}).");
        }
    }
}
