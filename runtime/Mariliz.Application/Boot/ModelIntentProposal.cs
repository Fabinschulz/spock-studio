namespace Mariliz.Application.Boot;

/// <summary>
/// O que o modelo propôs, já conferido contra o catálogo: os ids existem, o formato é o do contrato e o texto livre dele não está
/// aqui. Uma proposta que falhou carrega só o código do motivo (nunca a mensagem do provedor).
/// </summary>
/// <param name="State"><see cref="ModelStages.Used"/> ou <see cref="ModelStages.Failed"/>.</param>
/// <param name="CapabilityId">A capacidade principal, ou nulo quando o modelo disse que nenhuma serve.</param>
/// <param name="Alternatives">Até três outras que também cabem, sem repetir a principal.</param>
public sealed record ModelIntentProposal(string State, string? CapabilityId, IReadOnlyList<string> Alternatives, BootModelUsage Usage, string? FailureCode)
{
    public static ModelIntentProposal Failure(string code, BootModelUsage? usage = null) =>
        new(ModelStages.Failed, null, [], usage ?? new BootModelUsage(0, 0, 0), code);

    public int Proposed => (CapabilityId is null ? 0 : 1) + Alternatives.Count;
}
