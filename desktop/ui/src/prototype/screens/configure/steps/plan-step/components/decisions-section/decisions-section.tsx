import { StatusGlyph, type GlyphState } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { COMPONENT_KIND_TEXT, DECISION_TEXT, DECISION_TONE } from '@/prototype/constants/plan';
import screen from '@/prototype/styles/screen.module.scss';

import type { DecisionsSectionProps } from './types';

const decisionGlyph = (decision: string): GlyphState =>
  decision === 'skip' ? 'skipped' : decision === 'blocked' ? 'failed' : 'done';

const componentName = (componentId: string) => componentId.split('.').slice(2).join('.') || componentId;

export function DecisionsSection({ decisions }: DecisionsSectionProps) {
  return (
    <Section title={`Decisões por componente (${decisions.length})`} id="decisoes">
      <ul role="list" className={screen.rows}>
        {decisions.map((d) => (
          <li key={d.componentId} className={screen.row}>
            <span className={screen.rowIcon}>
              <StatusGlyph state={decisionGlyph(d.decision)} label={DECISION_TEXT[d.decision] ?? d.decision} />
            </span>
            <span>
              <span className={screen.rowTitle}>{componentName(d.componentId)}</span>
              <span className={screen.rowMeta}> · {COMPONENT_KIND_TEXT[d.kind]}</span>
              <span className={`${screen.rowMeta} ${screen.block}`}>{d.justification}</span>
            </span>
            <Tag tone={DECISION_TONE[d.decision] ?? 'neutral'}>{DECISION_TEXT[d.decision] ?? d.decision}</Tag>
          </li>
        ))}
      </ul>
    </Section>
  );
}
