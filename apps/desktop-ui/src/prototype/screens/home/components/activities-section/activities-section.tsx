import { Link } from 'react-router';

import { Glyph } from '@/shared/components/ui';

import { Section } from '@/prototype/components/section';
import { Tag } from '@/prototype/components/tag';
import { UPCOMING_ACTIVITIES } from '@/prototype/constants/home';
import screen from '@/prototype/styles/screen.module.scss';

export function ActivitiesSection() {
  return (
    <Section title="Atividades" id="atividades">
      <ul role="list" className={screen.rows}>
        <li>
          <Link to="/configurar" className={screen.row}>
            <span className={`${screen.rowIcon} ${screen.rowAccent}`}>
              <Glyph name="layers" size={18} />
            </span>
            <span>
              <span className={screen.rowTitle}>Configurar projeto</span>
              <span className={screen.rowMeta}> — analisa o repositório e configura o Harness.</span>
            </span>
            <span className={screen.rowEnd}>
              <Glyph name="arrow-right" size={16} />
            </span>
          </Link>
        </li>
        {UPCOMING_ACTIVITIES.map((item) => (
          <li key={item.title}>
            <div className={`${screen.row} ${screen.rowDisabled}`} aria-disabled="true">
              <span className={screen.rowIcon}>
                <Glyph name={item.glyph} size={18} />
              </span>
              <span>
                <span className={screen.rowTitle}>{item.title}</span>
                <span className={screen.rowMeta}> — {item.text}</span>
              </span>
              <Tag>em breve</Tag>
            </div>
          </li>
        ))}
      </ul>
    </Section>
  );
}
