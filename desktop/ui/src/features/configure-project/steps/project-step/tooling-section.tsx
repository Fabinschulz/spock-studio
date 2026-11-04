import type { Discovery } from '@/shared/schema';

import { toolKindLabel } from '../../domain/labels';
import styles from './discovery.module.scss';

export function ToolingSection({ tooling }: { tooling: Discovery['tooling'] }) {
  if (tooling.length === 0) return null;

  return (
    <section aria-labelledby="comandos-titulo" className={styles.section}>
      <h3 id="comandos-titulo" className={styles.title}>
        Comandos detectados
      </h3>
      <p className={styles.note}>Nada é executado por ter sido detectado.</p>
      <ul role="list" className={styles.rows}>
        {tooling.map((tool) => (
          <li key={tool.kind + tool.name}>
            <div className={styles.line}>
              <span>
                <span className={styles.name}>{toolKindLabel[tool.kind] ?? tool.kind}</span>{' '}
                {tool.command ? (
                  <span className={styles.mono}>{tool.command}</span>
                ) : (
                  <span className={styles.meta}>{tool.name}</span>
                )}
              </span>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
