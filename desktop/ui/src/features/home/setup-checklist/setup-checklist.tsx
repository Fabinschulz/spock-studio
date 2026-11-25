import { StatusGlyph } from '@/shared/components';
import { Button, LoadingBar } from '@mariliz/ui-kit';
import { useId } from 'react';
import { Link } from 'react-router';

import styles from './setup-checklist.module.scss';
import { countDone, SETUP_STEPS, type SetupStatus, type SetupStep } from './steps';

interface SetupChecklistProps {
  status: SetupStatus;
  /** Escolhe a pasta do projeto; a tela de configuração abre em seguida. */
  onPickProject: () => void;
  onDismiss: () => void;
}

/** O caminho de quem está começando, com o que já foi feito marcado a partir do que o motor confirma. */
export function SetupChecklist({ status, onPickProject, onDismiss }: SetupChecklistProps) {
  const titleId = useId();
  const done = countDone(status);

  return (
    <section className={styles.section} aria-labelledby={titleId}>
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          Primeiros passos
        </h2>
        <p className={styles.progress}>
          {done} de {SETUP_STEPS.length} concluídos
        </p>
        <Button variant="text" color="inherit" size="small" onClick={onDismiss}>
          Ocultar
        </Button>
      </div>
      <LoadingBar
        variant="determinate"
        value={(done / SETUP_STEPS.length) * 100}
        aria-label="Progresso dos primeiros passos"
      />
      <ol className={styles.steps}>
        {SETUP_STEPS.map((step) => (
          <li key={step.id} className={styles.step}>
            <Step
              step={step}
              done={status[step.id] === true}
              blocked={step.id === 'prepare' && !status.project}
              onPickProject={onPickProject}
            />
          </li>
        ))}
      </ol>
    </section>
  );
}

interface StepProps {
  step: SetupStep;
  done: boolean;
  /** Depende de um passo anterior: a ação só aparece quando ele estiver feito. */
  blocked: boolean;
  onPickProject: () => void;
}

function Step({ step, done, blocked, onPickProject }: StepProps) {
  return (
    <>
      <StatusGlyph state={done ? 'done' : 'pending'} label={done ? 'Concluído' : 'A fazer'} />
      <div className={styles.stepText}>
        <p className={styles.stepTitle}>
          {step.title}
          {step.optional ? <span className={styles.optional}> · opcional</span> : null}
        </p>
        <p className={styles.stepDescription}>{step.description}</p>
      </div>
      {done ? null : blocked ? (
        <p className={styles.blocked}>Depois de abrir um projeto</p>
      ) : step.id === 'project' ? (
        <Button variant="outlined" size="small" onClick={onPickProject}>
          {step.action}
        </Button>
      ) : (
        <Link to={step.to} className={styles.stepLink}>
          {step.action}
        </Link>
      )}
    </>
  );
}
