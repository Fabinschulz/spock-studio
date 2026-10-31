import { OperationStepRow } from '@/prototype/components/operation-step-row';
import { formatClock } from '@/prototype/domain/format-clock';

import styles from './operation-timeline.module.scss';
import type { OperationTimelineProps } from './types';

export function OperationTimeline({ view, clock, proposed }: OperationTimelineProps) {
  const done = view.steps.filter((s) => s.state === 'done' || s.state === 'warning').length;

  return (
    <div className={styles.op}>
      <div className={styles.summary}>
        {/* Anuncia a etapa atual, não cada arquivo gravado. */}
        <p className={styles.headline} role="status" aria-live="polite">
          {view.headline}
        </p>
        <p className={styles.elapsed}>
          <span className="mz-vh">Tempo decorrido </span>
          {formatClock(clock)}
        </p>
      </div>

      <div className={styles.segments} aria-hidden="true">
        {view.steps.map((s) => (
          <span key={s.id} className={styles.segment} data-state={s.state} />
        ))}
      </div>
      <p className="mz-vh">
        {done} de {view.steps.length} etapas concluídas.
      </p>

      <ol role="list" className={styles.steps} aria-label="Etapas da execução">
        {view.steps.map((step) => (
          <OperationStepRow key={step.id} step={step} proposed={proposed} />
        ))}
      </ol>
    </div>
  );
}
