import { EvidenceDisclosure, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { confidenceLabel, confidenceTone } from '../../domain/labels';

export function StacksSection({ stacks }: { stacks: Discovery['stacks'] }) {
  return (
    <SectionCard title="Stack detectada">
      {stacks.length === 0 ? (
        <p className={styles.muted}>Nenhuma stack conhecida foi detectada pelos arquivos de configuração.</p>
      ) : (
        <ul className={styles.list} aria-label="Stacks detectadas">
          {stacks.map((stack) => (
            <li key={stack.id + stack.root} className={styles.stack}>
              <div className={styles.row}>
                <span className={styles.cardTitle}>{stack.name}</span>
                <StatusChip tone="info">{stack.version ?? 'versão desconhecida'}</StatusChip>
                <StatusChip tone={confidenceTone[stack.confidence]}>
                  confiança {confidenceLabel[stack.confidence]}
                </StatusChip>
              </div>
              <span className={styles.muted}>Em {stack.root === '' ? 'raiz do projeto' : stack.root}</span>
              {stack.notes.length > 0 ? (
                <div className={styles.chips}>
                  {stack.notes.map((note) => (
                    <StatusChip key={note}>{note}</StatusChip>
                  ))}
                </div>
              ) : null}
              <EvidenceDisclosure
                summary={`Evidências (${stack.evidence.length})`}
                items={stack.evidence.map((e) => (e.detail ? `${e.path}: ${e.detail}` : e.path))}
              />
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}
