import { Home01Icon, LibraryIcon, ListViewRectangleIcon, SettingIcon } from '@synthra.io/ui-kit';
import { useId, type ReactNode } from 'react';
import { NavLink } from 'react-router';

import { useProjectSelectionContext } from '@/features/project-selection';
import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';

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

/** Só destinos que existem funcionam. O resto aparece como indisponível, com o motivo dito em texto. */
export function SideNav() {
  const { selected } = useProjectSelectionContext();

  const items: NavItem[] = [
    { id: 'home', label: 'Início', icon: <Home01Icon fontSize="small" />, to: pathTo('home'), end: true },
    {
      id: 'configure',
      label: 'Projetos',
      icon: <ListViewRectangleIcon fontSize="small" />,
      to: selected ? pathTo('configure') : undefined,
      unavailableReason: selected ? undefined : 'Selecione uma pasta'
    },
    { id: 'catalog', label: 'Catálogo', icon: <LibraryIcon fontSize="small" />, unavailableReason: 'Em breve' },
    { id: 'settings', label: 'Configurações', icon: <SettingIcon fontSize="small" />, unavailableReason: 'Em breve' }
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

  // Indisponível mas focável (aria-disabled), para o leitor de tela anunciar o motivo.
  return (
    <button
      type="button"
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
    </button>
  );
}
