import { Glyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { CONFIDENCE_TEXT, CONFIDENCE_TONE } from '@/prototype/constants/project';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

export function StackFindings() {
  const stack = discovery.stacks[0];

  return (
    <Section title="Stack" id="stack">
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
            <Tag tone={CONFIDENCE_TONE[stack.confidence]}>{CONFIDENCE_TEXT[stack.confidence]}</Tag>
          </li>
        ) : null}
      </ul>
      {stack ? (
        <details className={screen.details}>
          <summary>Evidências ({stack.evidence.length})</summary>
          <ul role="list" className={screen.stack}>
            {stack.evidence.map((e) => (
              <li key={e.path} className={screen.mono}>
                {e.path}
              </li>
            ))}
          </ul>
        </details>
      ) : null}
    </Section>
  );
}
