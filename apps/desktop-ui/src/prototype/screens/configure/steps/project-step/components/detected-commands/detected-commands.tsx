import { Glyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { TOOL_KIND_TEXT } from '@/prototype/constants/project';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

export function DetectedCommands() {
  return (
    <Section title="Comandos detectados" id="comandos" aside="nada é executado por ter sido detectado">
      <ul role="list" className={screen.rows}>
        {discovery.tooling.map((tool) => (
          <li key={tool.kind} className={screen.row}>
            <span className={screen.rowIcon}>
              <Glyph name="clock" size={18} />
            </span>
            <span>
              <span className={screen.rowTitle}>{TOOL_KIND_TEXT[tool.kind] ?? tool.kind}</span>{' '}
              <span className={screen.mono}>{tool.command}</span>
            </span>
            <span />
          </li>
        ))}
      </ul>
    </Section>
  );
}
