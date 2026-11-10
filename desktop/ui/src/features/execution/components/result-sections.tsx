import { InlineNotice } from '@/shared/components/ui';

import { ACTION_STATUS_LABEL, AREA_LABEL } from '../domain/labels';
import { failureHint, riskLabel } from '../domain/run-workspace';
import type { WorkflowSnapshot } from '../domain/run-timeline';
import styles from './workspace.module.scss';

type Result = NonNullable<WorkflowSnapshot['result']>;

export function FailureNotice({ error }: { error: NonNullable<Result['error']> }) {
  const hint = failureHint(error.code);
  return (
    <InlineNotice tone="danger" title="A execução falhou">
      <p className={styles.noticeText}>{error.message}</p>
      {hint ? <p className={styles.noticeText}>{hint}</p> : null}
      <p className={styles.noticeText}>
        Código: <span className={styles.mono}>{error.code}</span>
      </p>
    </InlineNotice>
  );
}

export function Timeline({ items }: { items: readonly { seq: number; text: string }[] }) {
  return (
    <section aria-labelledby="linha-titulo">
      <h2 id="linha-titulo">O que aconteceu</h2>
      <ol className={styles.list} aria-live="polite">
        {items.map((item) => (
          <li key={item.seq} className={styles.row}>
            {item.text}
          </li>
        ))}
      </ol>
    </section>
  );
}

export function ImpactSection({ impact }: { impact: NonNullable<Result['impact']> }) {
  return (
    <section aria-labelledby="impacto-titulo">
      <h2 id="impacto-titulo">Impacto técnico</h2>
      <ul className={styles.list}>
        {impact.areas.map((area) => (
          <li key={area}>{AREA_LABEL[area] ?? area}</li>
        ))}
      </ul>
      {impact.risks.length > 0 ? (
        <>
          <h3 className={styles.subtitle}>Riscos</h3>
          <ul className={styles.list}>
            {impact.risks.map((risk) => (
              <li key={risk}>{riskLabel(risk)}</li>
            ))}
          </ul>
        </>
      ) : null}
    </section>
  );
}

export function UsageSection({ usage }: { usage: NonNullable<Result['usage']> }) {
  return (
    <section aria-labelledby="modelo-titulo">
      <h2 id="modelo-titulo">Uso do modelo</h2>
      <p>
        {usage.modelCalls} chamada(s) · {usage.inputTokens} tokens de entrada · {usage.outputTokens} de saída. Contado
        pelo motor.
      </p>
    </section>
  );
}

export function LimitationsSection({ limitations }: { limitations: Result['limitations'] }) {
  return (
    <section aria-labelledby="limites-titulo">
      <h2 id="limites-titulo">Limitações</h2>
      <ul className={styles.list}>
        {limitations.map((item) => (
          <li key={item.code}>{item.message}</li>
        ))}
      </ul>
    </section>
  );
}

export function ActionOutcomes({ actions }: { actions: NonNullable<Result['actions']> }) {
  return (
    <section aria-labelledby="acoes-resultado-titulo">
      <h2 id="acoes-resultado-titulo">Ações</h2>
      <ul className={styles.list}>
        {actions.map((action) => (
          <li key={action.id} className={styles.row}>
            {action.summary}: {ACTION_STATUS_LABEL[action.status] ?? action.status}
            {action.target ? <span className={styles.path}> {action.target}</span> : null}
          </li>
        ))}
      </ul>
    </section>
  );
}
