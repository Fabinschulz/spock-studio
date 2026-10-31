import { CommandDisplay, SectionCard, StatusChip } from '@/shared/components/ui';
import type { Discovery } from '@/shared/schema';

import styles from '../../components/panels.module.scss';
import { toolKindLabel } from '../../domain/labels';

export function ToolingSection({ tooling }: { tooling: Discovery['tooling'] }) {
  if (tooling.length === 0) return null;

  const commands = tooling.filter((tool) => tool.command);
  const others = tooling.filter((tool) => !tool.command);

  return (
    <SectionCard title="Ferramentas e comandos">
      <p className={styles.muted}>Detectados nos arquivos do projeto. Nada é executado por ter sido detectado.</p>
      {commands.length > 0 ? (
        <ul className={styles.list} aria-label="Comandos detectados">
          {commands.map((tool) => (
            <li key={tool.kind + tool.name}>
              <CommandDisplay command={tool.command!} kind={toolKindLabel[tool.kind] ?? tool.kind} />
            </li>
          ))}
        </ul>
      ) : null}
      {others.length > 0 ? (
        <ul className={styles.chips} aria-label="Outras ferramentas detectadas">
          {others.map((tool) => (
            <li key={tool.kind + tool.name}>
              <StatusChip>
                {toolKindLabel[tool.kind] ?? tool.kind}: {tool.name}
              </StatusChip>
            </li>
          ))}
        </ul>
      ) : null}
    </SectionCard>
  );
}
