import { Fragment } from 'react';

import { Disclosure, StatusChip } from '@/shared/components/ui';
import type { ConfigurationPlan } from '@/shared/schema';
import { plural } from '@/shared/utils';

import rows from '../../components/rows.module.scss';
import { componentKindLabel, decisionLabel, decisionTone, originLabel } from '../../domain/labels';
import styles from './decisions.module.scss';

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
  return [plural(decisions.length, 'decisão', 'decisões'), ...parts].join(' · ');
}

/**
 * Três grupos: o que impede um componente de entrar (aberto), o que entra (aberto) e o que não se aplica (recolhido). A
 * justificativa é o texto principal; o id fica em segundo plano.
 */
export function DecisionsList({ decisions }: { decisions: ConfigurationPlan['decisions'] }) {
  const blocked = decisions.filter((d) => d.decision === 'blocked');
  const included = decisions.filter((d) => d.decision === 'enable' || d.decision === 'keep');
  const skipped = decisions.filter((d) => !blocked.includes(d) && !included.includes(d));

  return (
    <section aria-labelledby="decisoes-titulo" className={rows.section}>
      <h3 id="decisoes-titulo" className={rows.title}>
        Decisões por componente
      </h3>
      <p className={rows.note}>{decisionsSummary(decisions)}</p>
      {blocked.length > 0 ? (
        <Disclosure summary={plural(blocked.length, 'Bloqueada', 'Bloqueadas')} defaultOpen>
          <DecisionRows decisions={blocked} />
        </Disclosure>
      ) : null}
      {included.length > 0 ? (
        <Disclosure summary={`Entram (${included.length})`} defaultOpen>
          <DecisionRows decisions={included} />
        </Disclosure>
      ) : null}
      {skipped.length > 0 ? (
        <Disclosure summary={`Não se aplicam (${skipped.length})`}>
          <DecisionRows decisions={skipped} />
        </Disclosure>
      ) : null}
    </section>
  );
}

function DecisionRows({ decisions }: { decisions: readonly Decision[] }) {
  return (
    <ul role="list" className={rows.rows}>
      {decisions.map((decision) => (
        <li key={decision.componentId}>
          <details className={styles.details} open={decision.decision === 'blocked' || undefined}>
            <summary className={styles.summary}>
              <span>
                <span className={rows.name}>{decision.justification}</span>
                <span className={`${rows.meta} ${styles.block}`}>
                  {componentKindLabel[decision.kind]} ·{' '}
                  <span className={rows.mono}>
                    <BreakableId id={decision.componentId} />
                  </span>
                </span>
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
  );
}
