import { Disclosure } from '@/shared/components';
import type { ProjectMetrics } from '@/shared/schema';

import styles from '../management.module.scss';

/** O que o produto ainda não calcula não muda decisão do dia a dia: fica recolhido, mas nunca some nem vira zero. */
export function NotCalculated({ metrics }: { metrics: ProjectMetrics }) {
  if (metrics.notCalculated.length === 0) return null;

  return (
    <Disclosure className={styles.method} summary="O que ainda não é calculado">
      <ul className={styles.plain}>
        {metrics.notCalculated.map((entry) => (
          <li key={entry.id}>
            <span className={styles.rowTitle}>{entry.label}.</span> <span className={styles.muted}>{entry.reason}</span>
          </li>
        ))}
      </ul>
    </Disclosure>
  );
}
