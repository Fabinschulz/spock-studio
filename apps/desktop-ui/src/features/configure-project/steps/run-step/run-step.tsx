import { Alert, Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { PrimaryActionFooter } from '@/shared/components/ui';
import { formatElapsed } from '@/shared/utils';

import { RunStepRow } from '../../components/run-step-row';
import { StepHeader } from '../../components/step-header';
import panels from '../../components/panels.module.scss';
import { runKindLabel } from '../../domain/labels';
import { runHeadline } from '../../domain/run-headline';
import { useElapsed } from '../../hooks/use-elapsed';
import styles from './run-step.module.scss';
import type { RunStepProps } from './types';

export function RunStep({ run, onCancel, onBack }: RunStepProps) {
  const [cancelling, setCancelling] = useState(false);
  const failed = run.error !== null;
  const elapsed = useElapsed(run.startedAt, !failed);

  const cancel = async () => {
    setCancelling(true);
    await onCancel();
  };

  return (
    <section className={panels.step}>
      <StepHeader title={`${runKindLabel[run.kind]} em andamento`}>
        Acompanhe cada etapa. Se algo falhar ou você cancelar, o que já foi feito é desfeito.
      </StepHeader>

      <div className={styles.summary}>
        <p className={styles.headline} role="status" aria-live="polite">
          {runHeadline(run)}
        </p>
        <p className={styles.elapsed}>
          <span className="visually-hidden">Tempo decorrido </span>
          {formatElapsed(elapsed)}
        </p>
      </div>

      {run.steps.length > 0 ? (
        <div className={styles.segments} aria-hidden="true">
          {run.steps.map((step) => (
            <span key={step.id} className={styles.segment} data-status={step.status} />
          ))}
        </div>
      ) : null}

      <ol className={styles.steps} aria-label="Etapas da execução">
        {run.steps.map((step) => (
          <RunStepRow key={step.id} step={step} />
        ))}
      </ol>

      {run.files.length > 0 ? (
        <details className={panels.details}>
          <summary>{run.files.length} arquivo(s) processado(s)</summary>
          <ul className={panels.list}>
            {run.files.map((file, index) => (
              <li key={`${file}-${index}`} className={panels.mono}>
                {file}
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      {failed ? (
        <Alert severity="error" title="Não foi possível acompanhar a execução" description={run.error} />
      ) : null}

      <PrimaryActionFooter>
        {failed ? (
          <Button variant="outlined" color="inherit" onClick={onBack}>
            Voltar à revisão
          </Button>
        ) : (
          <Button variant="outlined" color="inherit" disabled={cancelling || run.runId === null} onClick={cancel}>
            {cancelling ? 'Cancelando…' : 'Cancelar e desfazer'}
          </Button>
        )}
      </PrimaryActionFooter>
    </section>
  );
}
