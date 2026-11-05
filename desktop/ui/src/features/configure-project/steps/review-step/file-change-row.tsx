import { DiffView } from '@/shared/components/ui';
import type { ConflictResolution, FileChange } from '@/shared/schema';

import { CheckboxField } from '../../components/choice-field';
import { actionLabel, componentKindLabel, originLabel } from '../../domain/labels';
import { ConflictResolutionField } from './conflict-resolution';
import styles from './review.module.scss';

interface FileChangeRowProps {
  change: FileChange;
  approved: boolean;
  resolution: ConflictResolution;
  onToggle: () => void;
  onResolve: (value: ConflictResolution) => void;
}

/** Uma linha por arquivo: a aprovação e o estado na própria linha; motivo e diferenças ficam ao alcance, sem cartão. */
export function FileChangeRow({ change, approved, resolution, onToggle, onResolve }: FileChangeRowProps) {
  const isConflict = change.action === 'conflict';
  const state =
    change.destructive && !isConflict ? `${actionLabel[change.action]} · destrutivo` : actionLabel[change.action];

  return (
    <li>
      <div className={styles.line}>
        {isConflict ? (
          <span className={styles.mono}>{change.path}</span>
        ) : (
          <CheckboxField
            plain
            checked={approved}
            onChange={onToggle}
            label={`Aprovar ${actionLabel[change.action].toLowerCase()} ${change.path}`}
          />
        )}
        <span className={`${styles.end} ${change.destructive || isConflict ? styles.danger : ''}`}>{state}</span>
      </div>
      <p className={styles.meta}>
        {componentKindLabel[change.kind]} · {originLabel[change.origin] ?? change.origin}
      </p>
      <p>{change.reason}</p>

      {isConflict ? <ConflictResolutionField change={change} resolution={resolution} onResolve={onResolve} /> : null}

      {change.diff ? (
        <details className={styles.details}>
          <summary className={styles.summary}>Ver diferenças</summary>
          <div className={styles.body}>
            <DiffView diff={change.diff} label={change.path} />
          </div>
        </details>
      ) : null}
    </li>
  );
}
