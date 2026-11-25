import { Fragment } from 'react';

import { StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';

import rows from '../../components/rows.module.scss';
import { componentKindLabel, decisionLabel, decisionTone, originLabel } from '../../domain/labels';
import styles from './plan.module.scss';

type Decision = ConfigurationPlan['decisions'][number];

/** Permite a quebra de linha depois de cada ponto do id; `<wbr>` só sugere o ponto, o texto copiado é o id exato. */
function BreakableId({ id }: { id: string }) {
  return id.split('.').map((part, index) => (
    <Fragment key={index}>
      {index > 0 ? (
        <>
          .<wbr />
        </>
      ) : null}
      {part}
    </Fragment>
  ));
}

export function decisionsSummary(decisions: readonly Decision[]): string {
  const counts = new Map<string, number>();
  for (const { decision } of decisions) counts.set(decision, (counts.get(decision) ?? 0) + 1);
  const parts = [...counts].map(
    ([decision, count]) => `${count} ${(decisionLabel[decision] ?? decision).toLowerCase()}`
  );
  return [`${decisions.length} ${decisions.length === 1 ? 'decisão' : 'decisões'}`, ...parts].join(' · ');
}

/** O que impede o componente de entrar já aparece aberto; o resto fica recolhido. */
const needsAttention = (decision: Decision) => decision.decision === 'blocked';

export function DecisionsList({ decisions }: { decisions: ConfigurationPlan['decisions'] }) {
  return (
    <section aria-labelledby="decisoes-titulo" className={rows.section}>
      <h3 id="decisoes-titulo" className={rows.title}>
        Decisões por componente
      </h3>
      <p className={rows.note}>{decisionsSummary(decisions)}</p>
      <ul role="list" className={rows.rows}>
        {decisions.map((decision) => (
          <li key={decision.componentId}>
            <details className={styles.details} open={needsAttention(decision) || undefined}>
              <summary className={styles.summary}>
                <span>
                  <span className={rows.name}>{componentKindLabel[decision.kind]}</span>
                  <span className={`${rows.meta} ${rows.mono}`}>
                    {' '}
                    <BreakableId id={decision.componentId} />
                  </span>
                  <span className={`${rows.meta} ${styles.block}`}>{decision.justification}</span>
                </span>
                <StatusChip tone={decisionTone[decision.decision] ?? 'neutral'}>
                  {decisionLabel[decision.decision] ?? decision.decision}
                </StatusChip>
              </summary>
              <div className={styles.body}>
                <p className={rows.meta}>Origem: {originLabel[decision.origin] ?? decision.origin}</p>
                {decision.evidence.length > 0 ? (
                  <ul role="list" className={styles.evidence}>
                    {decision.evidence.map((line, index) => (
                      // A evidência pode repetir o mesmo texto; a lista é estática, então a posição é a chave.
                      <li key={`${index}:${line}`}>{line}</li>
                    ))}
                  </ul>
                ) : (
                  <p className={rows.meta}>Sem evidências registradas.</p>
                )}
              </div>
            </details>
          </li>
        ))}
      </ul>
    </section>
  );
}
