import { Section } from '@/prototype/components/section';
import screen from '@/prototype/styles/screen.module.scss';

import type { RisksSectionProps } from './types';

export function RisksSection({ risks }: RisksSectionProps) {
  return (
    <Section title="Riscos" id="riscos" aside={`${risks.length}`}>
      <ul role="list" className={screen.stack}>
        {risks.map((risk) => (
          <li key={risk} className={screen.rowMeta}>
            {risk}
          </li>
        ))}
      </ul>
    </Section>
  );
}
