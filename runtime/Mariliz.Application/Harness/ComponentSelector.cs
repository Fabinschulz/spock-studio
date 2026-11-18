using Mariliz.Core.Plan;

namespace Mariliz.Application.Harness;

// Precedência: o que a pessoa excluiu na declaração, depois a proposta validada do Harness Engineer, depois as regras-base.
/// <param name="requested">Ainda não gravados na declaração.</param>
/// <param name="managed">Componentes que o lock já registra.</param>
/// <param name="retired">Componentes de uma IDE que saiu dos ambientes do projeto.</param>
internal sealed class ComponentSelector(
    IReadOnlySet<string> userExcluded,
    IReadOnlySet<string> requested,
    IReadOnlyDictionary<string, ProposalDecision> proposed,
    IReadOnlySet<string> managed,
    IReadOnlySet<string> retired,
    List<ComponentDecision> decisions,
    List<Limitation> limitations,
    string declaration)
{
    /// <param name="origin">De onde vem a decisão quando nem a pessoa nem a proposta decidiram.</param>
    public bool Wants(string id, bool applicable, string baselineReason, IReadOnlyList<string> baselineEvidence, string? notApplicableReason = null,
        string origin = DecisionOrigins.Baseline)
    {
        var kind = Catalog.Find(id)!.Kind;

        if (userExcluded.Contains(id))
            return Record(kind, id, ComponentDecisions.Skip, requested.Contains(id)
                ? $"Dispensado a seu pedido; o plano grava isto em {declaration} (components.exclude)."
                : $"Excluído em {declaration} (components.exclude).", DecisionOrigins.Declared, []);

        if (retired.Contains(id))
            return Record(kind, id, ComponentDecisions.Skip,
                $"A IDE deste componente não está mais entre os ambientes de IA de {declaration}; o que o Spock gravou para ela sai.", DecisionOrigins.Declared, []);

        proposed.TryGetValue(id, out var proposal);

        if (!applicable)
        {
            Record(kind, id, ComponentDecisions.Skip, notApplicableReason ?? "Não se aplica a este projeto.", DecisionOrigins.Baseline, []);
            if (proposal is { Decision: ComponentDecisions.Enable })
                limitations.Add(new(LimitationCodes.ProposalIgnored, $"A proposta pediu '{id}', mas ele não se aplica a este projeto; o pedido foi ignorado."));
            return false;
        }

        // "keep" é "já está adequado": de um componente que o Spock gerencia, ele continua desejado, com o mesmo conteúdo e
        // sem mudança no plano. Sem isto o componente saía do lock e deixava de ser conferido, revisado e revertido.
        if (proposal is not null)
            return Record(kind, id, proposal.Decision, proposal.Justification, DecisionOrigins.HarnessEngineer, proposal.Evidence)
                || (proposal.Decision == ComponentDecisions.Keep && managed.Contains(id));

        return Record(kind, id, ComponentDecisions.Enable, baselineReason, origin, baselineEvidence);
    }

    private bool Record(ComponentKind kind, string id, string decision, string justification, string origin, IReadOnlyList<string> evidence)
    {
        decisions.Add(new(kind, id, decision, justification, origin, evidence));
        return decision == ComponentDecisions.Enable;
    }
}
