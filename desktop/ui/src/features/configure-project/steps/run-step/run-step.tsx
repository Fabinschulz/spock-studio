import { Alert, Button } from '@mariliz/ui-kit';
import { useState } from 'react';

import { PrimaryActionFooter } from '@/shared/components/ui';
import { formatElapsed } from '@/shared/utils';

import { RunEvalList } from '../../components/run-eval-list';
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
  const [cancelError, setCancelError] = useState<string | null>(null);
  const failed = run.error !== null;
  const elapsed = useElapsed(run.startedAt, !failed);

  // Se o pedido de cancelamento falhar, o botão volta a funcionar e a pessoa vê o motivo.
  const cancel = async () => {
    setCancelling(true);
    setCancelError(null);
    try {
      await onCancel();
    } catch (error) {
      setCancelError(error instanceof Error ? error.message : 'Não foi possível cancelar a execução.');
      setCancelling(false);
    }
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

      <RunEvalList evals={run.evals} />

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

      {cancelError ? <Alert severity="error" title="Não foi possível cancelar" description={cancelError} /> : null}

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
