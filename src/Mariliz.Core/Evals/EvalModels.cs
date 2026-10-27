namespace Mariliz.Core.Evals;

/// <summary>
/// Contrato de resultado dos Evals. <c>Blocked</c> = não pôde rodar por falta de pré-condição;
/// <c>NotRun</c> = não foi executado nesta rodada. Nenhum dos dois conta como aprovado.
/// </summary>
public enum EvalStatus { Passed, Failed, Blocked, NotRun }

public enum EvalKind { Deterministic, Model }

public sealed record EvalResult(
    string Id,
    string Name,
    string Property,
    EvalKind Kind,
    bool Required,
    EvalStatus Status,
    string Criteria,
    IReadOnlyList<string> Evidence,
    string? Reason,
    IReadOnlyList<string> Limitations);

public enum ValidationOutcome { Passed, PassedWithLimitations, Failed }

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
