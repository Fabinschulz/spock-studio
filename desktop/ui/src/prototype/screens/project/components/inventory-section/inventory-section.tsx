import { StatusGlyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { INVENTORY_ROWS } from '@/prototype/constants/project';
import { discovery } from '@/prototype/data/fixtures';
import screen from '@/prototype/styles/screen.module.scss';

export function InventorySection() {
  return (
    <Section title="O que já existe" id="inventario" aside="encontrado na análise">
      <ul role="list" className={screen.rows}>
        {INVENTORY_ROWS.map(([label, key]) => {
          const items = discovery.inventory[key];
          const found = items.length > 0;

          return (
            <li key={key} className={screen.row}>
              <span className={screen.rowIcon}>
                <StatusGlyph state={found ? 'done' : 'skipped'} label={found ? 'encontrado' : 'nenhum'} />
              </span>
              <span>
                <span className={screen.rowTitle}>{label}</span>
                {found ? (
                  <span className={`${screen.rowMeta} ${screen.mono}`}> {items.map((i) => i.name).join(', ')}</span>
                ) : null}
              </span>
              <span className={screen.rowEnd}>
                {found ? `${items.length}` : 'nenhum'}
                {items.some((i) => !i.managed) ? <Tag>não gerenciado</Tag> : null}
              </span>
            </li>
          );
        })}
      </ul>
    </Section>
  );
}
