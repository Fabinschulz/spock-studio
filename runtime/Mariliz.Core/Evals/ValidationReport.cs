namespace Mariliz.Core.Evals;

public sealed record ValidationReport(IReadOnlyList<EvalResult> Results)
{
    /// <summary>
    /// Sucesso exige todos os Evals obrigatórios aprovados. Um obrigatório reprovado é falha; obrigatório
    /// bloqueado ou não executado é "com limitações". Um opcional que falhou também limita; um opcional que
    /// não rodou (como o juiz por modelo) aparece no relatório, mas não rebaixa o resultado.
    /// </summary>
    public ValidationOutcome Outcome
    {
        get
        {
            var required = Results.Where(r => r.Required).ToList();
            if (required.Any(r => r.Status == EvalStatus.Failed)) return ValidationOutcome.Failed;
            if (required.Any(r => r.Status is EvalStatus.Blocked or EvalStatus.NotRun)) return ValidationOutcome.PassedWithLimitations;
            if (Results.Any(r => !r.Required && r.Status is EvalStatus.Failed or EvalStatus.Blocked)) return ValidationOutcome.PassedWithLimitations;
            return ValidationOutcome.Passed;
        }
    }
}
