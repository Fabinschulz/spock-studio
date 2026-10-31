import { StatusGlyph } from '@/shared/components/ui';

import { stepStatusLabel } from '../../domain/labels';
import { glyphOfStatus } from '../../domain/run-headline';
import styles from './run-step-row.module.scss';
import type { RunStepRowProps } from './types';

export function RunStepRow({ step }: RunStepRowProps) {
  const seconds = step.startedAt !== null && step.endedAt !== null ? (step.endedAt - step.startedAt) / 1000 : null;

  return (
    <li className={styles.step} data-status={step.status}>
      <StatusGlyph state={glyphOfStatus(step.status)} />
      <div className={styles.line}>
        <span className={styles.title}>{step.title}</span>
        {step.status === 'completed' ? null : <span className={styles.meta}>{stepStatusLabel(step.status)}</span>}
        {step.detail ? <span className={styles.meta}>{step.detail}</span> : null}
        {seconds !== null && seconds > 0 ? <span className={styles.time}>{seconds.toFixed(1)} s</span> : null}
      </div>
    </li>
  );
}
