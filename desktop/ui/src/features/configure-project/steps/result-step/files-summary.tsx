import { EvidenceDisclosure, StatusChip, type Tone } from '@/shared/components/ui';
import type { RunResult } from '@/shared/schema';
import { plural } from '@/shared/utils';

import styles from '../../components/panels.module.scss';

type Files = NonNullable<RunResult['files']>;

const fileGroups = (files: Files): { one: string; label: string; items: readonly string[]; tone: Tone }[] => [
  { one: 'criado', label: 'criados', items: files.created, tone: 'success' },
  { one: 'atualizado', label: 'atualizados', items: files.updated, tone: 'info' },
  { one: 'removido', label: 'removidos', items: files.removed, tone: 'warning' },
  { one: 'preservado', label: 'preservados', items: files.preserved, tone: 'neutral' },
  { one: 'mantido (seu)', label: 'mantidos (seus)', items: files.kept, tone: 'neutral' },
  { one: 'não aplicado', label: 'não aplicados', items: files.notApplied, tone: 'warning' }
];

export function FilesSummary({ files }: { files: Files }) {
  // Grupo vazio não vira selo: "0 removidos" em tom de alerta chamaria atenção para nada.
  const groups = fileGroups(files).filter(({ items }) => items.length > 0);

  return (
    <div className={styles.card}>
      <h3 className={styles.cardTitle}>Arquivos</h3>
      {groups.length > 0 ? (
        <div className={styles.chips}>
          {groups.map(({ one, label, items, tone }) => (
            <StatusChip key={label} tone={tone}>
              {plural(items.length, one, label)}
            </StatusChip>
          ))}
        </div>
      ) : (
        <p className={styles.muted}>Nenhum arquivo foi alterado.</p>
      )}
      {groups.map(({ label, items }) => (
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
