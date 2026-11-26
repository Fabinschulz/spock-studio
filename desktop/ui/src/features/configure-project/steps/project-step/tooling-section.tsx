import type { Discovery } from '@/shared/schema';
import { cn } from '@/shared/utils';

import { toolKindLabel } from '../../domain/labels';
import styles from '../../components/rows.module.scss';

export function ToolingSection({ tooling }: { tooling: Discovery['tooling'] }) {
  if (tooling.length === 0) return null;

  return (
    <section aria-labelledby="comandos-titulo" className={styles.section}>
      <h3 id="comandos-titulo" className={styles.title}>
        Comandos detectados
      </h3>
      <p className={styles.note}>Nada é executado por ter sido detectado.</p>
      <ul role="list" className={styles.rows}>
        {/* Dois módulos do projeto podem declarar a mesma ferramenta: a posição desempata a chave. */}
        {tooling.map((tool, index) => (
          <li key={`${tool.kind}:${tool.name}:${index}`}>
            <div className={styles.line}>
              <span className={styles.name}>{toolKindLabel[tool.kind] ?? tool.kind}</span>
              {tool.command ? (
                <code className={cn(styles.mono, styles.end)}>{tool.command}</code>
              ) : (
                <span className={styles.end}>{tool.name}</span>
              )}
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
