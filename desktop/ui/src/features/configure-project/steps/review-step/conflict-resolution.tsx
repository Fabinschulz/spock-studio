import { InlineNotice } from '@/shared/components/ui';
import type { ConflictResolution, FileChange } from '@/shared/schema';

import { ChoiceField } from '../../components/choice-field';
import styles from '../../components/panels.module.scss';

interface ConflictResolutionProps {
  change: FileChange;
  resolution: ConflictResolution;
  onResolve: (value: ConflictResolution) => void;
}

export function ConflictResolutionField({ change, resolution, onResolve }: ConflictResolutionProps) {
  const name = `resolution-${change.id}`;

  return (
    <>
      <InlineNotice tone="warning">{change.conflictReason ?? 'O arquivo atual é diferente do proposto.'}</InlineNotice>
      <fieldset className={styles.fieldset}>
        <legend>{`Decisão para ${change.path}`}</legend>
        <ChoiceField type="radio" name={name} checked={resolution === 'keep'} onChange={() => onResolve('keep')}>
          <span>Manter o meu arquivo (padrão)</span>
        </ChoiceField>
        <ChoiceField
          type="radio"
          name={name}
          checked={resolution === 'overwrite'}
          onChange={() => onResolve('overwrite')}
        >
          <span>Substituir pelo proposto (o meu fica guardado em backup e pode ser restaurado)</span>
        </ChoiceField>
      </fieldset>
    </>
  );
}
