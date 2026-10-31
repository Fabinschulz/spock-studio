import { Alert, Button } from '@synthra.io/ui-kit';
import { useEffect, useState } from 'react';

import { PrimaryActionFooter, StatusGlyph, type GlyphState } from '@/shared/components/ui';

import panels from '../../components/panels.module.scss';
import { StepHeader } from '../../components/step-header';
import type { RunStepView, RunView } from '../../domain/journey-state';
import { formatElapsed, runningStep, stepProgress } from '../../domain/run-progress';
import { runKindLabel, stepStatusLabel } from '../../domain/labels';
import styles from './run-step.module.scss';

interface RunStepProps {
  run: RunView;
  onCancel: () => Promise<void> | void;
  onBack: () => void;
}

const glyphOf = (status: string): GlyphState =>
  status === 'pending'
    ? 'pending'
    : status === 'running'
      ? 'running'
      : status === 'completed'
        ? 'done'
        : status === 'failed'
          ? 'failed'
          : 'warning';

/** Tempo desde o início, medido aqui: o motor ainda não carimba os eventos. Só corre enquanto há execução. */
function useElapsed(startedAt: number, active: boolean) {
  const [now, setNow] = useState(startedAt);
  useEffect(() => {
    if (!active) return;
    const tick = () => setNow(Date.now());
    const id = window.setInterval(tick, 1000);
    return () => window.clearInterval(id);
  }, [active]);
  return Math.max(0, now - startedAt);
}

function StepRow({ step }: { step: RunStepView }) {
  const seconds = step.startedAt !== null && step.endedAt !== null ? (step.endedAt - step.startedAt) / 1000 : null;
  return (
    <li className={styles.step} data-status={step.status}>
      <StatusGlyph state={glyphOf(step.status)} />
      <div className={styles.stepLine}>
        <span className={styles.stepTitle}>{step.title}</span>
        {step.status === 'completed' ? null : <span className={styles.stepMeta}>{stepStatusLabel(step.status)}</span>}
        {step.detail ? <span className={styles.stepMeta}>{step.detail}</span> : null}
        {seconds !== null && seconds > 0 ? <span className={styles.stepTime}>{seconds.toFixed(1)} s</span> : null}
      </div>
    </li>
  );
}

/** O progresso vem dos eventos do motor: nenhuma etapa aparece como feita antes de ele dizer que foi. */
export function RunStep({ run, onCancel, onBack }: RunStepProps) {
  const [cancelling, setCancelling] = useState(false);
  const failed = run.error !== null;
  const elapsed = useElapsed(run.startedAt, !failed);
  const { done, total } = stepProgress(run);
  const running = runningStep(run);

  const cancel = async () => {
    setCancelling(true);
    await onCancel();
  };

  const headline = running
    ? total > 0
      ? `${running.title} · etapa ${Math.min(done + 1, total)} de ${total}`
      : `${running.title}…`
    : failed
      ? 'A execução não pôde ser acompanhada'
      : 'Aguardando o motor…';

  return (
    <section className={panels.step}>
      <StepHeader title={`${runKindLabel[run.kind]} em andamento`}>
        Acompanhe cada etapa. Se algo falhar ou você cancelar, o que já foi feito é desfeito.
      </StepHeader>

      <div className={styles.summary}>
        {/* Região viva: anuncia a etapa atual, não cada arquivo gravado. */}
        <p className={styles.headline} role="status" aria-live="polite">
          {headline}
        </p>
        <p className={styles.elapsed}>
          <span className="visually-hidden">Tempo decorrido </span>
          {formatElapsed(elapsed)}
        </p>
      </div>

      {total > 0 ? (
        // Um segmento por etapa: não é percentual (o motor não informa percentuais).
        <div className={styles.segments} aria-hidden="true">
          {run.steps.map((s) => (
            <span key={s.id} className={styles.segment} data-status={s.status} />
          ))}
        </div>
      ) : null}

      <ol className={styles.steps} aria-label="Etapas da execução">
        {run.steps.map((step) => (
          <StepRow key={step.id} step={step} />
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
