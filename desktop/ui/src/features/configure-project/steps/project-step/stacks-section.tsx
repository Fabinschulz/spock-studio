import { EvidenceDisclosure } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import { confidenceLabel } from '../../domain/labels';
import styles from './discovery.module.scss';

export function StacksSection({ stacks }: { stacks: Discovery['stacks'] }) {
  return (
    <section aria-labelledby="stack-titulo" className={styles.section}>
      <h3 id="stack-titulo" className={styles.title}>
        Stack
      </h3>
      {stacks.length === 0 ? (
        <p className={styles.note}>Nenhuma stack conhecida foi detectada pelos arquivos de configuração.</p>
      ) : (
        <ul role="list" className={styles.rows}>
          {stacks.map((stack) => (
            <li key={stack.id + stack.root}>
              <div className={styles.line}>
                <span>
                  <span className={styles.name}>{stack.name}</span>
                  <span className={styles.meta}>
                    {' '}
                    {stack.version ?? 'versão desconhecida'} · em {stack.root === '' ? 'raiz do projeto' : stack.root}
                  </span>
                </span>
                <span className={styles.end}>confiança {confidenceLabel[stack.confidence]}</span>
              </div>
              {stack.notes.map((note) => (
                <div key={note} className={styles.meta}>
                  {note}
                </div>
              ))}
              <EvidenceDisclosure
                summary={`Evidências (${stack.evidence.length})`}
                items={stack.evidence.map((e) => (e.detail ? `${e.path}: ${e.detail}` : e.path))}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
