import screen from '@/prototype/styles/screen.module.scss';

import type { ConflictResolutionProps } from './types';

export function ConflictResolution({ path, value, onChange }: ConflictResolutionProps) {
  return (
    <div className={screen.indent} role="group" aria-label={`Resolução do conflito em ${path}`}>
      <div className={screen.segmented}>
        <button type="button" aria-pressed={value === 'keep'} onClick={() => onChange('keep')}>
          Manter o seu arquivo
        </button>
        <button type="button" aria-pressed={value === 'overwrite'} onClick={() => onChange('overwrite')}>
          Substituir (com backup)
        </button>
      </div>
    </div>
  );
}
