import { DiffView, Disclosure, StatusChip } from '@/shared/components/ui';
import type { ConflictResolution, FileChange } from '@/shared/schema';

import { CheckboxField } from '../../components/choice-field';
import rows from '../../components/rows.module.scss';
import { actionLabel, actionTone, originLabel } from '../../domain/labels';
import { ConflictResolutionField } from './conflict-resolution';

interface FileChangeRowProps {
  change: FileChange;
  approved: boolean;
  resolution: ConflictResolution;
  onToggle: () => void;
  onResolve: (value: ConflictResolution) => void;
}

export function FileChangeRow({ change, approved, resolution, onToggle, onResolve }: FileChangeRowProps) {
  const isConflict = change.action === 'conflict';
  const state =
    change.destructive && !isConflict ? `${actionLabel[change.action]} · destrutivo` : actionLabel[change.action];

  return (
    <li>
      <div className={rows.line}>
        {isConflict ? (
          <span className={rows.mono}>{change.path}</span>
        ) : (
          <CheckboxField plain checked={approved} onChange={onToggle} label={change.path} />
        )}
        <StatusChip tone={change.destructive && !isConflict ? 'danger' : actionTone[change.action]}>{state}</StatusChip>
      </div>
      <p className={rows.meta}>Origem: {originLabel[change.origin] ?? change.origin}</p>
      <p>{change.reason}</p>

      {isConflict ? <ConflictResolutionField change={change} resolution={resolution} onResolve={onResolve} /> : null}

      {change.diff ? (
        <Disclosure summary="Ver diferenças">
          <DiffView diff={change.diff} label={change.path} />
        </Disclosure>
      ) : null}
    </li>
  );
}
