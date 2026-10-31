import { NavLink } from 'react-router';

import { Glyph, Logo, Wordmark } from '@/shared/components/ui';

import { NAV_ITEMS, NAV_SOON_ITEMS } from '@/prototype/constants/shell';

import styles from './rail.module.scss';

export function Rail() {
  return (
    <aside className={styles.rail}>
      <NavLink to="/" className={styles.brand} aria-label="Mariliz Studio, início">
        <Logo />
        <Wordmark className={styles.wordmark} />
      </NavLink>

      <nav aria-label="Navegação principal" className={styles.nav}>
        <ul role="list">
          {NAV_ITEMS.map((item) => (
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
          {NAV_SOON_ITEMS.map((item) => (
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
