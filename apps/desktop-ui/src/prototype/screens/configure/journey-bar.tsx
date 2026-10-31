import { Glyph } from '../../ui/glyph';

import styles from './journey-bar.module.scss';
import { STEPS, type StepSlug } from './journey';

interface JourneyBarProps {
  current: number;
  /** Etapa com problema real (falha ou bloqueio). */
  issue?: { index: number; label: string };
  onGo: (slug: StepSlug) => void;
}

/**
 * Onde estou, o que já foi feito e o que vem a seguir. Etapas concluídas voltam; as futuras não são controles
 * (as dependências entre etapas são do motor). O texto de cada estado é lido por tecnologia assistiva.
 */
export function JourneyBar({ current, issue, onGo }: JourneyBarProps) {
  return (
    <nav className={styles.bar} aria-label="Etapas da configuração">
      <ol role="list" className={styles.list}>
        {STEPS.map((step, i) => {
          const state = issue?.index === i ? 'issue' : i < current ? 'done' : i === current ? 'current' : 'todo';
          const text = {
            issue: issue?.label ?? 'com problema',
            done: 'concluída',
            current: 'etapa atual',
            todo: 'pendente'
          }[state];
          const body = (
            <>
              <span className={styles.dot} aria-hidden="true">
                {state === 'done' ? <Glyph name="check" size={11} /> : state === 'issue' ? '!' : i + 1}
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
