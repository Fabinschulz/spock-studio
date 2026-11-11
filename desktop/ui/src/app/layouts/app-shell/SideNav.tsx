import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';
import {
  Button,
  ChartBarIcon,
  GridIcon,
  Home01Icon,
  IntersectingArrowsIcon,
  LibraryIcon,
  ListViewRectangleIcon,
  SettingIcon
} from '@mariliz/ui-kit';
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

/** Um ambiente do produto. Sem rótulo, o grupo é do aplicativo como um todo. */
interface NavGroup {
  id: string;
  label?: string;
  items: NavItem[];
}

// Engenharia e Gestão são visões do mesmo projeto; Integrações alimenta as duas e por isso não pertence a nenhuma.
const GROUPS: NavGroup[] = [
  {
    id: 'app',
    items: [{ id: 'home', label: 'Início', icon: <Home01Icon fontSize="small" />, to: pathTo('home'), end: true }]
  },
  {
    id: 'engineering',
    label: 'Engenharia',
    items: [
      { id: 'project', label: 'Projeto', icon: <ListViewRectangleIcon fontSize="small" />, to: pathTo('project') },
      {
        id: 'configure',
        label: 'Configurar',
        icon: <ListViewRectangleIcon fontSize="small" />,
        to: pathTo('configure')
      },
      { id: 'catalog', label: 'Catálogo', icon: <LibraryIcon fontSize="small" />, unavailableReason: 'Em breve' }
    ]
  },
  {
    id: 'management',
    label: 'Gestão',
    items: [
      { id: 'portfolio', label: 'Portfólio', icon: <GridIcon fontSize="small" />, to: pathTo('portfolio') },
      { id: 'management', label: 'Acompanhamento', icon: <ChartBarIcon fontSize="small" />, to: pathTo('management') }
    ]
  },
  {
    id: 'platform',
    label: 'Plataforma',
    items: [
      {
        id: 'integrations',
        label: 'Integrações',
        icon: <IntersectingArrowsIcon fontSize="small" />,
        to: pathTo('integrations')
      },
      { id: 'settings', label: 'Configurações', icon: <SettingIcon fontSize="small" />, to: pathTo('settings') }
    ]
  }
];

export function SideNav() {
  return (
    <nav aria-label="Navegação principal" className={styles.groups}>
      {GROUPS.map((group) => (
        <NavGroupList key={group.id} group={group} />
      ))}
    </nav>
  );
}

function NavGroupList({ group }: { group: NavGroup }) {
  const labelId = useId();

  return (
    <div className={styles.group}>
      {group.label ? (
        <p id={labelId} className={styles.groupLabel}>
          {group.label}
        </p>
      ) : null}
      <ul className={styles.nav} aria-labelledby={group.label ? labelId : undefined}>
        {group.items.map((item) => (
          <li key={item.id}>
            <NavEntry item={item} />
          </li>
        ))}
      </ul>
    </div>
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
