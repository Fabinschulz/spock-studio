import { EvidenceDisclosure, StatusChip, type Tone } from '@/shared/components/ui';
import type { RunResult } from '@/shared/schema';

import styles from '../../components/panels.module.scss';

type Files = NonNullable<RunResult['files']>;

const fileGroups = (files: Files): { label: string; items: readonly string[]; tone: Tone }[] => [
  { label: 'criados', items: files.created, tone: 'success' },
  { label: 'atualizados', items: files.updated, tone: 'info' },
  { label: 'removidos', items: files.removed, tone: 'warning' },
  { label: 'preservados', items: files.preserved, tone: 'neutral' },
  { label: 'mantidos (seus)', items: files.kept, tone: 'neutral' },
  { label: 'não aplicados', items: files.notApplied, tone: 'warning' }
];

export function FilesSummary({ files }: { files: Files }) {
  const groups = fileGroups(files);

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Arquivos</h3>
      <div className={styles.chips}>
        {groups.map(({ label, items, tone }) => (
          <StatusChip key={label} tone={tone}>
            {items.length} {label}
          </StatusChip>
        ))}
      </div>
      {groups
        .filter(({ items }) => items.length > 0)
        .map(({ label, items }) => (
          <EvidenceDisclosure key={label} summary={`${label} (${items.length})`} items={items} />
        ))}
      {files.commandEffects.length > 0 ? (
        <EvidenceDisclosure
          summary={`Efeitos do instalador (${files.commandEffects.length})`}
          items={files.commandEffects}
        />
      ) : null}
    </div>
  );
}
