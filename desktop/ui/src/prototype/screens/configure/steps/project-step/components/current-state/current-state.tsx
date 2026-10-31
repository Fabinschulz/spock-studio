import { Glyph, StatusGlyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

export function CurrentState() {
  const existing = Object.values(discovery.inventory).reduce((sum, list) => sum + list.length, 0);

  return (
    <Section title="Estado atual" id="estado">
      <ul role="list" className={screen.rows}>
        <li className={screen.row}>
          <span className={screen.rowIcon}>
            <StatusGlyph state="skipped" label="não instalada" />
          </span>
          <span className={screen.rowTitle}>Estrutura de especificação</span>
          <Tag>não instalada</Tag>
        </li>
        <li className={screen.row}>
          <span className={screen.rowIcon}>
            <Glyph name="layers" size={18} />
          </span>
          <span>
            <span className={screen.rowTitle}>Harness existente</span>
            <span className={screen.rowMeta}> {existing} item(ns): 1 agente, 1 regra</span>
          </span>
          <span />
        </li>
        {discovery.warnings.map((warning) => (
          <li key={warning.code} className={screen.row}>
            <span className={screen.rowIcon}>
              <StatusGlyph state="warning" />
            </span>
            <span>
              <span className={screen.rowTitle}>Não é um repositório git</span>
              <span className={screen.rowMeta}> — a reversão usará o backup da aplicação.</span>
            </span>
            <span />
          </li>
        ))}
      </ul>
    </Section>
  );
}
