import { StatusGlyph } from '@/shared/components/ui';

import { Tag } from '@/prototype/components/tag';
import { EVAL_PROGRESS_GLYPH, EVAL_PROGRESS_TEXT } from '@/prototype/constants/evals';

import styles from './eval-progress-list.module.scss';
import type { EvalProgressListProps } from './types';

export function EvalProgressList({ evals }: EvalProgressListProps) {
  return (
    <ul role="list" className={styles.evals}>
      {evals.map((e) => (
        <li key={e.id} className={styles.evalRow}>
          <StatusGlyph state={EVAL_PROGRESS_GLYPH[e.state]} label={EVAL_PROGRESS_TEXT[e.state]} />
          <span>{e.name}</span>
          {e.kind === 'model' ? <Tag>juiz por modelo</Tag> : null}
          <span className={styles.meta}>{EVAL_PROGRESS_TEXT[e.state]}</span>
        </li>
      ))}
    </ul>
  );
}
