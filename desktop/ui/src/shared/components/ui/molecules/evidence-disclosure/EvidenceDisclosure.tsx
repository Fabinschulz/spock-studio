import type { ReactNode } from 'react';
import styles from './EvidenceDisclosure.module.scss';

interface EvidenceDisclosureProps {
  summary: string;
  items: readonly string[];
  children?: ReactNode;
}

export function EvidenceDisclosure({ summary, items, children }: EvidenceDisclosureProps) {
  if (items.length === 0 && !children) return null;
  return (
    <details className={styles.details}>
      <summary className={styles.summary}>{summary}</summary>
      <div className={styles.content}>
        {children}
        <ul className={styles.list}>
          {items.map((item) => (
            <li key={item} className={styles.item}>
              {item}
            </li>
          ))}
        </ul>
      </div>
    </details>
  );
}
