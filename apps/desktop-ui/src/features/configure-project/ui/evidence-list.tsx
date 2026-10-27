import type { ReactNode } from 'react';

import styles from './panels.module.scss';

interface EvidenceListProps {
  summary: string;
  items: readonly string[];
  children?: ReactNode;
}

/** Evidências ficam recolhidas: quem quer conferir abre, quem só lê o resumo não é cercado de caminhos. */
export function EvidenceList({ summary, items, children }: EvidenceListProps) {
  if (items.length === 0 && !children) return null;
  return (
    <details className={styles.details}>
      <summary>{summary}</summary>
      {children}
      <ul className={styles.list}>
        {items.map((item) => (
          <li key={item} className={styles.mono}>
            {item}
          </li>
        ))}
      </ul>
    </details>
  );
}
