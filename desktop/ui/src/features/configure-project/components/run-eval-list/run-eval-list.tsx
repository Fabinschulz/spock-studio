import { StatusGlyph } from '@/shared/components/ui';

import { evalStatusGlyphs } from '../../constants/eval-status-glyphs';
import { evalStatusLabel } from '../../domain/labels';
import styles from './run-eval-list.module.scss';
import type { RunEvalListProps } from './types';

const statusText = (status: string) =>
  status === 'running' ? 'em andamento' : (evalStatusLabel[status as keyof typeof evalStatusLabel] ?? status);

/** Uma linha por verificação, na ordem em que o motor as roda. Só aparece o que o motor anunciou. */
export function RunEvalList({ evals }: RunEvalListProps) {
  if (evals.length === 0) return null;

  return (
    <section aria-labelledby="verificacoes-titulo" className={styles.section}>
      <h3 id="verificacoes-titulo" className={styles.title}>
        Verificações
      </h3>
      <ul role="list" className={styles.rows}>
        {evals.map((item) => (
          <li key={item.id} className={styles.row} data-status={item.status}>
            <StatusGlyph state={evalStatusGlyphs[item.status] ?? 'warning'} label={statusText(item.status)} />
            <span className={styles.name}>{item.name}</span>
            {item.kind === 'model' ? <span className={styles.meta}>por modelo</span> : null}
            <span className={styles.end}>{statusText(item.status)}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
