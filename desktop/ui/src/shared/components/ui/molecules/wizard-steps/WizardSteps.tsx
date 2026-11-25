import { cn } from '@/shared/utils';
import { CheckGlyph } from '../../atoms/icons';
import styles from './WizardSteps.module.scss';

export interface WizardStep {
  id: string;
  label: string;
}

export interface StepIssue {
  stepId: string;
  kind: 'error' | 'blocked';
}

interface WizardStepsProps {
  steps: readonly WizardStep[];
  currentId: string;
  issue?: StepIssue;
  /** Etapas anteriores pelas quais a pessoa não passou (um atalho levou direto adiante): não aparecem como concluídas. */
  skippedIds?: readonly string[];
  /** As etapas futuras nunca são controles: não se pula o que ainda não foi feito. */
  onSelect?: (id: string) => void;
}

type State = 'done' | 'skipped' | 'current' | 'upcoming';

const spoken: Record<State | StepIssue['kind'], string> = {
  done: ' (concluída)',
  skipped: ' (não usada nesta visita)',
  current: ' (etapa atual)',
  upcoming: ' (pendente)',
  error: ' (com erro)',
  blocked: ' (bloqueada)'
};

/**
 * O estado de cada etapa é dito em texto e por forma (✓, número, !), nunca só por cor. Em janelas estreitas os rótulos
 * somem do desenho e entra "Etapa N de M"; para o leitor de tela a lista é a mesma.
 */
export function WizardSteps({ steps, currentId, issue, skippedIds = [], onSelect }: WizardStepsProps) {
  const currentIndex = steps.findIndex((s) => s.id === currentId);
  const current = steps[currentIndex];

  return (
    <div className={styles.root}>
      <ol className={styles.steps} aria-label="Etapas da configuração" role="list">
        {steps.map((step, index) => {
          const state: State =
            index === currentIndex
              ? 'current'
              : index > currentIndex
                ? 'upcoming'
                : skippedIds.includes(step.id)
                  ? 'skipped'
                  : 'done';
          const problem = issue?.stepId === step.id ? issue.kind : null;
          const content = (
            <>
              <span className={styles.marker} aria-hidden="true">
                {problem === 'error' ? (
                  '!'
                ) : problem === 'blocked' ? (
                  '×'
                ) : state === 'done' ? (
                  <CheckGlyph />
                ) : (
                  index + 1
                )}
              </span>
              <span className={styles.label}>
                {step.label}
                <span className="visually-hidden">{spoken[problem ?? state]}</span>
              </span>
            </>
          );
          return (
            <li
              key={step.id}
              className={cn(styles.step, styles[state], problem && styles[problem])}
              aria-current={state === 'current' ? 'step' : undefined}
            >
              {onSelect && state === 'done' ? (
                <button
                  type="button"
                  className={styles.target}
                  aria-label={`Voltar para a etapa ${step.label}`}
                  onClick={() => onSelect(step.id)}
                >
                  {content}
                </button>
              ) : (
                content
              )}
            </li>
          );
        })}
      </ol>
      {current ? (
        <p className={styles.summary} aria-hidden="true">
          Etapa {currentIndex + 1} de {steps.length} · <strong>{current.label}</strong>
        </p>
      ) : null}
    </div>
  );
}
