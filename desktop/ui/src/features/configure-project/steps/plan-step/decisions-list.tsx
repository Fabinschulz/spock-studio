import { Fragment } from 'react';

import type { ConfigurationPlan } from '@/shared/schema';

import { componentKindLabel, decisionLabel, originLabel } from '../../domain/labels';
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

/** "12 decisões · 8 habilitar · 3 manter": só as que existem, na ordem em que o motor as nomeia. */
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
    <section aria-labelledby="decisoes-titulo" className={styles.section}>
      <h3 id="decisoes-titulo" className={styles.title}>
        Decisões por componente
      </h3>
      <p className={styles.note}>{decisionsSummary(decisions)}</p>
      <ul role="list" className={styles.rows}>
        {decisions.map((decision) => (
          <li key={decision.componentId}>
            <details className={styles.details} open={needsAttention(decision) || undefined}>
              <summary className={styles.summary}>
                <span>
                  <span className={styles.name}>{componentKindLabel[decision.kind]}</span>
                  <span className={`${styles.meta} ${styles.mono}`}>
                    {' '}
                    <BreakableId id={decision.componentId} />
                  </span>
                  <span className={`${styles.meta} ${styles.block}`}>{decision.justification}</span>
                </span>
                <span className={styles.end}>{decisionLabel[decision.decision] ?? decision.decision}</span>
              </summary>
              <div className={styles.body}>
                <p className={styles.meta}>Origem: {originLabel[decision.origin] ?? decision.origin}</p>
                {decision.evidence.length > 0 ? (
                  <ul role="list" className={styles.evidence}>
                    {decision.evidence.map((line, index) => (
                      // A evidência pode repetir o mesmo texto; a lista é estática, então a posição é a chave.
                      <li key={`${index}:${line}`}>{line}</li>
                    ))}
                  </ul>
                ) : (
                  <p className={styles.meta}>Sem evidências registradas.</p>
                )}
              </div>
            </details>
          </li>
        ))}
      </ul>
    </section>
  );
}
