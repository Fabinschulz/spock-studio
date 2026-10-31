import { StatusGlyph } from '@/shared/components/ui';

import { EvalProgressList } from '@/prototype/components/eval-progress-list';
import { FileList } from '@/prototype/components/file-list';
import { STEP_GLYPH } from '@/prototype/constants/steps';

import styles from './operation-step-row.module.scss';
import type { OperationStepRowProps } from './types';

const VALIDATE_STEP_ID = 'validate';

export function OperationStepRow({ step, proposed }: OperationStepRowProps) {
  const seconds = step.startedAt !== null && step.endedAt !== null ? (step.endedAt - step.startedAt) / 1000 : null;
  const validating = step.id === VALIDATE_STEP_ID && step.state !== 'pending';

  return (
    <li className={styles.step} data-state={step.state}>
      <StatusGlyph state={STEP_GLYPH[step.state]} />
      <div className={styles.stepBody}>
        <div className={styles.stepLine}>
          <p className={styles.stepTitle}>{step.title}</p>
          {step.expected && step.state === 'pending' ? <span className={styles.stepMeta}>aguardando</span> : null}
          {step.detail ? <span className={styles.stepMeta}>{step.detail}</span> : null}
          {seconds !== null ? <span className={styles.stepTime}>{seconds.toFixed(1)} s</span> : null}
        </div>

        {step.files.length > 0 ? <FileList files={step.files} collapsible={step.state !== 'running'} /> : null}

        {validating && proposed ? <EvalProgressList evals={step.evals} /> : null}
        {validating && !proposed && step.state === 'running' ? (
          <p className={styles.honest}>
            O motor publica a validação como uma única etapa. O resultado de cada verificação chega quando ela termina.
          </p>
        ) : null}
      </div>
    </li>
  );
}
