import { NavLink } from 'react-router';

import { Wordmark, Logo } from '@/shared/components/ui';

import { Glyph, type GlyphName } from '../ui/glyph';

import styles from './shell.module.scss';

interface Item {
  label: string;
  glyph: GlyphName;
  to?: string;
  end?: boolean;
}

// Só entram itens que têm função hoje. O que ainda não existe aparece como "Em breve", dito em texto.
const items: Item[] = [
  { label: 'Início', glyph: 'home', to: '/', end: true },
  { label: 'Projeto', glyph: 'folder', to: '/projeto' },
  { label: 'Configurar', glyph: 'layers', to: '/configurar' }
];

const soon: Item[] = [
  { label: 'Catálogo', glyph: 'file' },
  { label: 'Configurações', glyph: 'settings' }
];

export function Rail() {
  return (
    <aside className={styles.rail}>
      <NavLink to="/" className={styles.brand} aria-label="Mariliz Studio, início">
        <Logo />
        <Wordmark className={styles.wordmark} />
      </NavLink>

      <nav aria-label="Navegação principal" className={styles.nav}>
        <ul role="list">
          {items.map((item) => (
            <li key={item.label}>
              <NavLink
                to={item.to ?? '/'}
                end={item.end}
                className={({ isActive }) => `${styles.item} ${isActive ? styles.itemActive : ''}`}
              >
                <Glyph name={item.glyph} size={17} />
                <span className={styles.itemLabel}>{item.label}</span>
              </NavLink>
            </li>
          ))}
        </ul>
        <ul role="list" className={styles.soonList} aria-label="Em breve">
          {soon.map((item) => (
            <li key={item.label}>
              <button type="button" className={`${styles.item} ${styles.itemSoon}`} aria-disabled="true">
                <Glyph name={item.glyph} size={17} />
                <span className={styles.itemLabel}>{item.label}</span>
                <span className={styles.soonTag}>em breve</span>
              </button>
            </li>
          ))}
        </ul>
      </nav>

      <div className={styles.engine} role="status">
        <span className={styles.engineDot} aria-hidden="true" />
        <span className={styles.itemLabel}>
          Motor local <span className={styles.engineState}>pronto</span>
        </span>
      </div>
    </aside>
  );
}
