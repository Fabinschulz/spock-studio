import { Alert, Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { PrimaryActionFooter, StatusChip } from '@/shared/components/ui';

import styles from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { RunView } from '../../domain/journey-state';
import { runKindLabel, runStateLabel, stepStatusLabel, stepStatusTone } from '../../domain/labels';

interface RunStepProps {
  run: RunView;
  onCancel: () => Promise<void> | void;
  onBack: () => void;
}

/** O progresso vem dos eventos do motor: nenhuma etapa aparece como feita antes de ele dizer que foi. */
export function RunStep({ run, onCancel, onBack }: RunStepProps) {
  const [cancelling, setCancelling] = useState(false);
  const failed = run.error !== null;

  const cancel = async () => {
    setCancelling(true);
    await onCancel();
  };

  return (
    <section className={styles.step}>
      <StepHeader title={`${runKindLabel[run.kind]} em andamento`}>
        Acompanhe cada etapa. Se algo falhar ou você cancelar, o que já foi feito é desfeito.
      </StepHeader>

      <div role="status" aria-live="polite" className={styles.card}>
        <div className={styles.cardTitle}>
          Estado: <StatusChip tone={failed ? 'danger' : 'info'}>{runStateLabel[run.state] ?? run.state}</StatusChip>
        </div>
        {failed ? null : <div className={styles.progress} />}
        <ol className={styles.list} aria-label="Etapas da execução">
          {run.steps.map((step) => (
            <li key={step.id} className={styles.row}>
              <StatusChip tone={stepStatusTone(step.status)}>{stepStatusLabel(step.status)}</StatusChip>
              <span>{step.title}</span>
              {step.detail ? <span className={styles.muted}>{step.detail}</span> : null}
            </li>
          ))}
        </ol>
        {run.files.length > 0 ? (
          <details className={styles.details}>
            <summary>{run.files.length} arquivo(s) processado(s)</summary>
            <ul className={styles.list}>
              {run.files.map((file, index) => (
                <li key={`${file}-${index}`} className={styles.mono}>
                  {file}
                </li>
              ))}
            </ul>
          </details>
        ) : null}
      </div>

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
