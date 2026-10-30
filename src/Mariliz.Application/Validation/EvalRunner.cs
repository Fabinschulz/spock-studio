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
        new RulesCheck(), new ReferencesCheck(), new TlcCheck(), new SecretsCheck(), new EnvironmentCheck(),
        new RequirementsCheck(), new IdempotenceCheck(), new RegressionCheck(), new ModelJudgeCheck()
    }.ToDictionary(check => check.Id, StringComparer.Ordinal);

    public static async Task<ValidationReport> RunAsync(ValidationContext ctx, CancellationToken ct)
    {
        var requiredByDeclaration = ctx.Declared?.Evals.Required.ToHashSet(StringComparer.Ordinal) ?? [];
        var results = new List<EvalResult>();

        foreach (var spec in EvalCatalog.Specs)
        {
            ct.ThrowIfCancellationRequested();
            var result = await RunCheckAsync(spec, ctx, results, ct);
            results.Add(result with { Required = spec.Required || requiredByDeclaration.Contains(spec.Id) });
        }

        return new ValidationReport(results);
    }

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
