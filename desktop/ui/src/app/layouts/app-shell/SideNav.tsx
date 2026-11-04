import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';
import { Button, Home01Icon, LibraryIcon, ListViewRectangleIcon, SettingIcon } from '@synthra.io/ui-kit';
import { useId, type ReactNode } from 'react';
import { NavLink } from 'react-router';
import styles from './AppShell.module.scss';

interface NavItem {
  id: string;
  label: string;
  icon: ReactNode;
  /** Sem destino = indisponível; `unavailableReason` diz por quê. */
  to?: string;
  end?: boolean;
  unavailableReason?: string;
}

export function SideNav() {
  const items: NavItem[] = [
    { id: 'home', label: 'Início', icon: <Home01Icon fontSize="small" />, to: pathTo('home'), end: true },
    { id: 'configure', label: 'Projetos', icon: <ListViewRectangleIcon fontSize="small" />, to: pathTo('configure') },
    { id: 'catalog', label: 'Catálogo', icon: <LibraryIcon fontSize="small" />, unavailableReason: 'Em breve' },
    { id: 'settings', label: 'Configurações', icon: <SettingIcon fontSize="small" />, to: pathTo('settings') }
  ];

  return (
    <nav aria-label="Navegação principal">
      <ul className={styles.nav}>
        {items.map((item) => (
          <li key={item.id}>
            <NavEntry item={item} />
          </li>
        ))}
      </ul>
    </nav>
  );
}

function NavEntry({ item }: { item: NavItem }) {
  const reasonId = useId();

  if (item.to && !item.unavailableReason) {
    return (
      <NavLink to={item.to} end={item.end} className={({ isActive }) => cn(styles.item, isActive && styles.active)}>
        <span className={styles.itemIcon} aria-hidden="true">
          {item.icon}
        </span>
        <span className={styles.itemText}>
          <span className={styles.itemLabel}>{item.label}</span>
        </span>
      </NavLink>
    );
  }

  return (
    <Button
      variant="text"
      color="inherit"
      className={cn(styles.item, styles.unavailable)}
      aria-disabled="true"
      aria-describedby={reasonId}
    >
      <span className={styles.itemIcon} aria-hidden="true">
        {item.icon}
      </span>
      <span className={styles.itemText}>
        <span className={styles.itemLabel}>{item.label}</span>
        <span id={reasonId} className={styles.itemReason}>
          {item.unavailableReason}
        </span>
      </span>
    </Button>
  );
}
