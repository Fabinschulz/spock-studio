import { Glyph } from '@/shared/components/ui';

import { STEPS } from '@/prototype/constants/journey-steps';

import styles from './journey-bar.module.scss';
import type { JourneyBarProps, JourneyItemState } from './types';

const STATE_TEXT: Record<Exclude<JourneyItemState, 'issue'>, string> = {
  done: 'concluída',
  current: 'etapa atual',
  todo: 'pendente'
};

function itemState(index: number, current: number, issueIndex: number | undefined): JourneyItemState {
  if (issueIndex === index) return 'issue';
  if (index < current) return 'done';
  return index === current ? 'current' : 'todo';
}

// Etapas concluídas voltam; as futuras não são controles (as dependências são do motor).
export function JourneyBar({ current, issue, onGo }: JourneyBarProps) {
  return (
    <nav className={styles.bar} aria-label="Etapas da configuração">
      <ol role="list" className={styles.list}>
        {STEPS.map((step, index) => {
          const state = itemState(index, current, issue?.index);
          const text = state === 'issue' ? (issue?.label ?? 'com problema') : STATE_TEXT[state];
          const marker = state === 'done' ? <Glyph name="check" size={11} /> : state === 'issue' ? '!' : index + 1;

          const body = (
            <>
              <span className={styles.dot} aria-hidden="true">
                {marker}
              </span>
              <span className={styles.label}>{step.label}</span>
              <span className="mz-vh">, {text}</span>
            </>
          );

          return (
            <li
              key={step.slug}
              className={styles.item}
              data-state={state}
              aria-current={state === 'current' ? 'step' : undefined}
            >
              {state === 'done' ? (
                <button type="button" className={styles.step} onClick={() => onGo(step.slug)}>
                  {body}
                </button>
              ) : (
                <span className={styles.step}>{body}</span>
              )}
            </li>
          );
        })}
      </ol>
      <p className={styles.compact} aria-hidden="true">
        Etapa {current + 1} de {STEPS.length} · <strong>{STEPS[current]?.label}</strong>
      </p>
    </nav>
  );
}
