import { Fact } from '@/prototype/components/fact';
import { Section } from '@/prototype/components/section';

import styles from './summary-section.module.scss';
import type { SummarySectionProps } from './types';

export function SummarySection({ facts }: SummarySectionProps) {
  return (
    <Section title="Resumo" id="resumo">
      <dl className={styles.facts}>
        {facts.map(([label, value]) => (
          <Fact key={label} label={label}>
            {value}
          </Fact>
        ))}
      </dl>
    </Section>
  );
}
