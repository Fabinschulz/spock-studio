import { EvidenceDisclosure, InlineNotice } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';

import rows from '../../components/rows.module.scss';
import { observationLabel } from '../../domain/labels';

/** O que dá para contar na aba: limitações, observações e riscos. */
export const risksCount = (plan: ConfigurationPlan): number =>
  plan.limitations.length + plan.observations.length + plan.risks.length;

/** Limitações, observações e riscos num lugar só: cada aviso aparece uma vez, aqui. */
export function RisksTab({ plan }: { plan: ConfigurationPlan }) {
  if (risksCount(plan) === 0) return <p className={rows.note}>O plano não registrou limitações nem riscos.</p>;

  return (
    <>
      {plan.limitations.length > 0 ? (
        <section aria-labelledby="limitacoes-titulo" className={rows.section}>
          <h3 id="limitacoes-titulo" className={rows.title}>
            Limitações
          </h3>
          {plan.limitations.map((limitation) => (
            <InlineNotice key={limitation.code + limitation.message} tone="warning">
              {limitation.message}
            </InlineNotice>
          ))}
        </section>
      ) : null}

      {plan.observations.length > 0 ? (
        <section aria-labelledby="observacoes-titulo" className={rows.section}>
          <h3 id="observacoes-titulo" className={rows.title}>
            Observações
          </h3>
          <ul role="list" className={rows.rows}>
            {plan.observations.map((observation) => (
              <li key={observation.kind + observation.text}>
                <div className={rows.line}>
                  <span>{observation.text}</span>
                  <span className={rows.end}>{observationLabel[observation.kind]}</span>
                </div>
                <EvidenceDisclosure
                  summary={`Evidências (${observation.evidence.length})`}
                  items={observation.evidence}
                />
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {plan.risks.length > 0 ? (
        <section aria-labelledby="riscos-titulo" className={rows.section}>
          <h3 id="riscos-titulo" className={rows.title}>
            Riscos
          </h3>
          <ul role="list" className={rows.rows}>
            {plan.risks.map((risk) => (
              <li key={risk}>{risk}</li>
            ))}
          </ul>
        </section>
      ) : null}
    </>
  );
}
