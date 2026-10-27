import { cn } from '@/shared/utils';

import styles from './WizardSteps.module.scss';

export interface WizardStep {
  id: string;
  label: string;
}

interface WizardStepsProps {
  steps: readonly WizardStep[];
  currentId: string;
}

/** Progresso da jornada. O passo atual usa aria-current; os concluídos e pendentes são ditos em texto para leitor de tela. */
export function WizardSteps({ steps, currentId }: WizardStepsProps) {
  const currentIndex = steps.findIndex((s) => s.id === currentId);

  return (
    <ol className={styles.steps} aria-label="Etapas da configuração" role="list">
      {steps.map((step, index) => {
        const state = index < currentIndex ? 'done' : index === currentIndex ? 'current' : 'upcoming';
        return (
          <li
            key={step.id}
            className={cn(styles.step, styles[state])}
            aria-current={state === 'current' ? 'step' : undefined}
          >
            <span className={styles.marker} aria-hidden="true">
              {state === 'done' ? '✓' : index + 1}
            </span>
            <span className={styles.label}>
              {step.label}
              <span className="visually-hidden">
                {state === 'done' ? ' (concluída)' : state === 'current' ? ' (etapa atual)' : ' (pendente)'}
              </span>
            </span>
          </li>
        );
      })}
    </ol>
  );
}
