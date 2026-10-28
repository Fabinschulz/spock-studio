import { Alert, Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { PrimaryActionFooter, StatusChip } from '@/shared/components/ui';

import type { RunView } from '../../core/journey-state';
import { runKindLabel } from '../../core/labels';
import styles from '../../ui/panels.module.scss';
import { StepHeader } from '../../ui/step-header';

interface RunStepProps {
  run: RunView;
  onCancel: () => Promise<void> | void;
  onBack: () => void;
}

const stateLabel: Record<string, string> = {
  running: 'Executando',
  validating: 'Validando',
  failed: 'Falhou'
};

const stepTone = (status: string) =>
  status === 'running' ? 'info' : status.startsWith('completed') ? 'success' : 'warning';
const stepLabel = (status: string) =>
  status === 'running' ? 'em andamento' : status === 'completed' ? 'concluída' : status.replace(/_/g, ' ');

/** O progresso vem dos eventos do motor: nenhuma etapa aparece como feita antes de ele dizer que foi. */
export function RunStep({ run, onCancel, onBack }: RunStepProps) {
  const [cancelling, setCancelling] = useState(false);
  const failed = run.error !== null;

  return (
    <section className={styles.step}>
      <StepHeader title={`${runKindLabel[run.kind]} em andamento`}>
        Acompanhe cada etapa. Se algo falhar ou você cancelar, o que já foi feito é desfeito.
      </StepHeader>

      <div role="status" aria-live="polite" className={styles.card}>
        <div className={styles.cardTitle}>
          Estado: <StatusChip tone={failed ? 'danger' : 'info'}>{stateLabel[run.state] ?? run.state}</StatusChip>
        </div>
        {failed ? null : <div className={styles.progress} />}
        <ol className={styles.list} aria-label="Etapas da execução">
          {run.steps.map((s) => (
            <li key={s.id} className={styles.row}>
              <StatusChip tone={stepTone(s.status)}>{stepLabel(s.status)}</StatusChip>
              <span>{s.title}</span>
              {s.detail ? <span className={styles.muted}>{s.detail}</span> : null}
            </li>
          ))}
        </ol>
        {run.files.length > 0 ? (
          <details className={styles.details}>
            <summary>{run.files.length} arquivo(s) processado(s)</summary>
            <ul className={styles.list}>
              {run.files.map((f, i) => (
                <li key={`${f}-${i}`} className={styles.mono}>
                  {f}
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
          <Button
            variant="outlined"
            color="inherit"
            disabled={cancelling || run.runId === null}
            onClick={async () => {
              setCancelling(true);
              await onCancel();
            }}
          >
            {cancelling ? 'Cancelando…' : 'Cancelar e desfazer'}
          </Button>
        )}
      </PrimaryActionFooter>
    </section>
  );
}
