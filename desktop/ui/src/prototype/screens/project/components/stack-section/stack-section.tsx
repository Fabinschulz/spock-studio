import { Glyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { TOOL_KIND_TEXT } from '@/prototype/constants/project';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

export function StackSection() {
  const stack = discovery.stacks[0];

  return (
    <Section title="Stack e comandos" id="stack">
      <ul role="list" className={screen.rows}>
        {stack ? (
          <li className={screen.row}>
            <span className={screen.rowIcon}>
              <Glyph name="cpu" size={18} />
            </span>
            <span>
              <span className={screen.rowTitle}>{stack.name}</span>
              <span className={screen.rowMeta}>
                {' '}
                {stack.version} · em {stack.root}
              </span>
            </span>
            <Tag tone="success">confiança alta</Tag>
          </li>
        ) : null}
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
