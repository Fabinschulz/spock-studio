import { PanelSection } from '../panel-section';
import styles from './summary-section.module.scss';
import type { SummarySectionProps } from './types';

export function SummarySection({ facts }: SummarySectionProps) {
  return (
    <PanelSection id="ctx-resumo" title="Resumo">
      <dl className={styles.facts}>
        {facts.map(([label, value]) => (
          <div key={label} className={styles.fact}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
    </PanelSection>
  );
}
