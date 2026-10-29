import { Button } from '@synthra.io/ui-kit';
import { useState } from 'react';

import { DiffView, StatusChip } from '@/shared/components/ui';
import type { ConflictResolution, FileChange } from '@/shared/schema';

import { ChoiceField } from '../../components/choice-field';
import styles from '../../components/panels.module.scss';
import { actionLabel, actionTone, componentKindLabel, originLabel } from '../../domain/labels';
import { ConflictResolutionField } from './conflict-resolution';

interface FileChangeCardProps {
  change: FileChange;
  approved: boolean;
  resolution: ConflictResolution;
  onToggle: () => void;
  onResolve: (value: ConflictResolution) => void;
}

export function FileChangeCard({ change, approved, resolution, onToggle, onResolve }: FileChangeCardProps) {
  const [showDiff, setShowDiff] = useState(false);

  return (
    <li className={styles.card}>
      <div className={styles.cardTitle}>
        <StatusChip tone={actionTone[change.action]}>{actionLabel[change.action]}</StatusChip>
        <span className={styles.mono}>{change.path}</span>
        {change.destructive && change.action !== 'conflict' ? <StatusChip tone="danger">destrutivo</StatusChip> : null}
      </div>
      <span className={styles.muted}>
        {componentKindLabel[change.kind]} · {originLabel[change.origin] ?? change.origin}
      </span>
      <span>{change.reason}</span>

      {change.action === 'conflict' ? (
        <ConflictResolutionField change={change} resolution={resolution} onResolve={onResolve} />
      ) : (
        <ChoiceField type="checkbox" checked={approved} onChange={onToggle}>
          <span>{`Aprovar ${actionLabel[change.action].toLowerCase()} ${change.path}`}</span>
        </ChoiceField>
      )}

      {change.diff ? (
        <>
          <Button variant="text" onClick={() => setShowDiff((current) => !current)} aria-expanded={showDiff}>
            {showDiff ? 'Ocultar diferenças' : 'Ver diferenças'}
          </Button>
          {showDiff ? <DiffView diff={change.diff} label={change.path} /> : null}
        </>
      ) : null}
    </li>
  );
}
