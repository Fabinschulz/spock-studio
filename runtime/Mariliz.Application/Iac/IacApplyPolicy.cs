using Mariliz.Application.Execution;

namespace Mariliz.Application.Iac;

// O que a pessoa precisa confirmar para aplicar um plano. A interface pergunta exatamente isto; o motor confere de novo.
public sealed record IacApplyRequirements(
    string PlanId,
    int Destructive,
    int Risks,
    string? WorkspaceConfirmation,
    DateTimeOffset ExpiresAt);

// A regra de quando um plano pode ser aplicado mora só aqui: o motor recusa o pedido com ela e a lista de operações
// anuncia com ela quais planos a interface pode oferecer, para as duas nunca divergirem.
public static class IacApplyPolicy
{
    // Plano velho tende a ser recusado pela ferramenta (state mudou); o Spock recusa antes, com uma mensagem clara.
    public static readonly TimeSpan PlanLifetime = TimeSpan.FromHours(24);

    private static readonly HashSet<string> ProductionWorkspaces = new(StringComparer.Ordinal) { "prd", "prod", "production", "producao" };

    public static bool IsPlan(IacOperation operation) =>
        operation is { Kind: IacOperationKind.Plan, Status: IacOperationStatus.Succeeded, PlanId: not null, Summary: not null };

    // O primeiro motivo, na ordem em que o pedido é conferido, pelo qual o plano não pode ser aplicado agora. O histórico
    // vem do mais recente para o mais antigo.
    public static EngineException? Refusal(IReadOnlyList<IacOperation> history, IacOperation plan, DateTimeOffset now)
    {
        if (!IsPlan(plan)) return IacErrors.NotAPlan();
        if (plan.AppliedBy is not null) return IacErrors.PlanAlreadyApplied();
        if (plan.HasChanges != true) return IacErrors.PlanNoChanges();
        if (plan.ApplyBlocker is { } blocker) return IacErrors.ApplyBlocked(blocker);
        // Qualquer plano antes deste no histórico, da mesma pasta e ambiente, é mais novo.
        if (history.TakeWhile(o => o.Id != plan.Id).Any(o => o.Kind == IacOperationKind.Plan && o.Status == IacOperationStatus.Succeeded
                                                          && o.Folder == plan.Folder && o.Workspace == plan.Workspace))
            return IacErrors.PlanSuperseded();
        if (now - plan.FinishedAt > PlanLifetime) return IacErrors.PlanExpired();
        return null;
    }

    // Nulo quando o plano não pode ser aplicado agora.
    public static IacApplyRequirements? Requirements(IReadOnlyList<IacOperation> history, IacOperation operation, DateTimeOffset now) =>
        Refusal(history, operation, now) is null ? For(operation) : null;

    public static IacApplyRequirements For(IacOperation plan)
    {
        var summary = plan.Summary ?? throw new InvalidOperationException("Só um plano tem requisitos de aplicação.");
        var risks = (plan.Findings ?? []).Count(f => f.Severity == IacFindingSeverity.Danger && f.Rule is not ("destroy" or "replace"));
        var production = plan.Workspace is { } ws && ProductionWorkspaces.Contains(ws) ? ws : null;
        return new IacApplyRequirements(plan.PlanId!, summary.Delete + summary.Replace, risks, production, plan.FinishedAt + PlanLifetime);
    }
}
