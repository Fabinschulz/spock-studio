namespace Mariliz.Application.Workflows;

// Sem ações, nenhuma é aprovada: o padrão é não executar.
public sealed record ResumeDecision(string? ApprovalId, IReadOnlyList<string> ActionIds)
{
    public static readonly ResumeDecision None = new(null, []);
}
