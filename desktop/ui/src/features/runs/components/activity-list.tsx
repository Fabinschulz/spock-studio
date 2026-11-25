import { StatusChip } from '@/shared/components';
import type { RunItem } from '@/shared/schema';
import { formatEngineDate, runKindLabel } from '@/shared/utils';

import { displayState, stateTone } from '../domain/run-labels';
import styles from './activity.module.scss';

interface ActivityListProps {
  label: string;
  runs: readonly RunItem[];
  onOpen: (run: RunItem) => void;
}

/** Cada linha abre o detalhe da execução, que só lê: nada aqui aprova, reverte nem dispara. */
export function ActivityList({ label, runs, onOpen }: ActivityListProps) {
  return (
    <ul className={styles.list} role="list" aria-label={label}>
      {runs.map((run) => (
        <li key={run.runId}>
          <button type="button" className={styles.row} aria-haspopup="dialog" onClick={() => onOpen(run)}>
            <span className={styles.rowText}>
              <span className={styles.rowTitle}>{runKindLabel(run.kind)}</span>
              <span className={styles.rowMeta}>
                {run.projectName} · {formatEngineDate(run.startedAt)}
              </span>
            </span>
            <StatusChip tone={stateTone(run)}>{displayState(run)}</StatusChip>
          </button>
        </li>
      ))}
    </ul>
  );
}
