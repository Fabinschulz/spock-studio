import { StatusGlyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { demoRuns } from '@/prototype/data/demo-project';
import screen from '@/prototype/styles/screen.module.scss';

export function RunsSection() {
  return (
    <Section title="Execuções" id="execucoes" aside="exemplo">
      <ul role="list" className={screen.rows}>
        {demoRuns.map((run) => (
          <li key={run.id} className={screen.row}>
            <span className={screen.rowIcon}>
              <StatusGlyph state={run.outcome === 'Concluído' ? 'done' : 'warning'} label={run.outcome} />
            </span>
            <span>
              <span className={screen.rowTitle}>{run.kind}</span>
              <span className={screen.rowMeta}> · {run.outcome}</span>
            </span>
            <span className={screen.rowEnd}>{run.when}</span>
          </li>
        ))}
      </ul>
    </Section>
  );
}
