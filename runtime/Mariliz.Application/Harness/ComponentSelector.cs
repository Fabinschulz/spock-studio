using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

/// <summary>
/// Decide se um componente entra no plano e registra a decisão. Precedência: o que a pessoa excluiu em mariliz.yaml,
/// depois a proposta validada do Harness Engineer, depois as regras-base.
/// </summary>
internal sealed class ComponentSelector(
    IReadOnlySet<string> userExcluded,
    IReadOnlyDictionary<string, ProposalDecision> proposed,
    List<ComponentDecision> decisions,
    List<Limitation> limitations)
{
    public bool Wants(string id, bool applicable, string baselineReason, IReadOnlyList<string> baselineEvidence, string? notApplicableReason = null)
    {
        var kind = Catalog.Find(id)!.Kind;

        if (userExcluded.Contains(id))
            return Record(kind, id, ComponentDecisions.Skip, "Excluído em mariliz.yaml (components.exclude).", DecisionOrigins.Declared, []);

        proposed.TryGetValue(id, out var proposal);

        if (!applicable)
        {
            Record(kind, id, ComponentDecisions.Skip, notApplicableReason ?? "Não se aplica a este projeto.", DecisionOrigins.Baseline, []);
            if (proposal is { Decision: ComponentDecisions.Enable })
                limitations.Add(new(LimitationCodes.ProposalIgnored, $"A proposta pediu '{id}', mas ele não se aplica a este projeto; o pedido foi ignorado."));
            return false;
        }

        if (proposal is not null)
            return Record(kind, id, proposal.Decision, proposal.Justification, DecisionOrigins.HarnessEngineer, proposal.Evidence);

        return Record(kind, id, ComponentDecisions.Enable, baselineReason, DecisionOrigins.Baseline, baselineEvidence);
    }

    private bool Record(ComponentKind kind, string id, string decision, string justification, string origin, IReadOnlyList<string> evidence)
    {
        decisions.Add(new(kind, id, decision, justification, origin, evidence));
        return decision == ComponentDecisions.Enable;
    }
}
