namespace Mariliz.Application.Workflows;

/// <summary>
/// A decisão de quem aprova: o <c>approvalId</c> que viu no evento de aprovação e as ações que aprova. Sem ações, nenhuma
/// é aprovada: o padrão é não executar.
/// </summary>
public sealed record ResumeDecision(string? ApprovalId, IReadOnlyList<string> ActionIds)
{
    public static readonly ResumeDecision None = new(null, []);
}
