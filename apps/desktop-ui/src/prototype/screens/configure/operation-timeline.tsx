import { formatClock } from '../../domain/describe-event';
import type { EvalProgress, OperationStep, OperationView, StepState } from '../../domain/operation-model';
import { StatusGlyph, Tag, type GlyphState } from '../../ui/primitives';

import styles from './operation.module.scss';

const glyphOf: Record<StepState, GlyphState> = {
  pending: 'pending',
  running: 'running',
  done: 'done',
  warning: 'warning',
  failed: 'failed'
};

const evalGlyph = (state: EvalProgress['state']): GlyphState =>
  state === 'running'
    ? 'running'
    : state === 'passed'
      ? 'done'
      : state === 'failed'
        ? 'failed'
        : state === 'blocked'
          ? 'warning'
          : 'skipped';

const evalText: Record<EvalProgress['state'], string> = {
  running: 'em andamento',
  passed: 'aprovado',
  failed: 'reprovado',
  blocked: 'bloqueado',
  not_run: 'não executado'
};

interface Props {
  view: OperationView;
  clock: number;
  proposed: boolean;
}

/** Linha do tempo da execução: uma linha por etapa, na ordem real. O que ainda não começou aparece como aguardando. */
export function OperationTimeline({ view, clock, proposed }: Props) {
  const total = view.steps.length;
  const done = view.steps.filter((s) => s.state === 'done' || s.state === 'warning').length;

  return (
    <div className={styles.op}>
      <div className={styles.summary}>
        {/* Região viva: anuncia a etapa atual, não cada arquivo gravado. */}
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
        {done} de {total} etapas concluídas.
      </p>

      <ol role="list" className={styles.steps} aria-label="Etapas da execução">
        {view.steps.map((step) => (
          <StepRow key={step.id} step={step} proposed={proposed} />
        ))}
      </ol>
    </div>
  );
}

function FileList({ files }: { files: string[] }) {
  return (
    <ul role="list" className={styles.files}>
      {files.map((file) => (
        <li key={file} className={styles.file}>
          <span className={styles.fileOp}>criado</span> {file}
        </li>
      ))}
    </ul>
  );
}

function StepRow({ step, proposed }: { step: OperationStep; proposed: boolean }) {
  const duration = step.startedAt !== null && step.endedAt !== null ? (step.endedAt - step.startedAt) / 1000 : null;
  return (
    <li className={styles.step} data-state={step.state}>
      <StatusGlyph state={glyphOf[step.state]} />
      <div className={styles.stepBody}>
        <div className={styles.stepLine}>
          <p className={styles.stepTitle}>{step.title}</p>
          {step.expected && step.state === 'pending' ? <span className={styles.stepMeta}>aguardando</span> : null}
          {step.detail ? <span className={styles.stepMeta}>{step.detail}</span> : null}
          {duration !== null ? <span className={styles.stepTime}>{duration.toFixed(1)} s</span> : null}
        </div>

        {step.files.length > 0 ? (
          // Enquanto grava, cada arquivo aparece; concluída, a etapa recolhe a lista num resumo que se pode abrir.
          step.state === 'running' ? (
            <FileList files={step.files} />
          ) : (
            <details className={styles.fileDetails}>
              <summary>{step.files.length} arquivos gravados</summary>
              <FileList files={step.files} />
            </details>
          )
        ) : null}

        {step.id === 'validate' && step.state !== 'pending' ? (
          proposed ? (
            <ul role="list" className={styles.evals}>
              {step.evals.map((e) => (
                <li key={e.id} className={styles.evalRow}>
                  <StatusGlyph state={evalGlyph(e.state)} label={evalText[e.state]} />
                  <span>{e.name}</span>
                  {e.kind === 'model' ? <Tag>juiz por modelo</Tag> : null}
                  <span className={styles.stepMeta}>{evalText[e.state]}</span>
                </li>
              ))}
            </ul>
          ) : step.state === 'running' ? (
            <p className={styles.honest}>
              O motor publica a validação como uma única etapa. O resultado de cada verificação chega quando ela
              termina.
            </p>
          ) : null
        ) : null}
      </div>
    </li>
  );
}
