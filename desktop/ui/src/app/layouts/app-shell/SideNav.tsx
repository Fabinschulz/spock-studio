import { pathTo } from '@/shared/routing';
import { cn } from '@/shared/utils';
import {
  AssignmentIcon,
  BookOpenIcon,
  BriefcaseIcon,
  Button,
  ChartBarIcon,
  ChatIcon,
  GridIcon,
  Home01Icon,
  IntersectingArrowsIcon,
  LibraryIcon,
  ListViewRectangleIcon,
  PlayIcon,
  SettingIcon,
  ShieldCheckIcon
} from '@mariliz/ui-kit';
import { useId, type ReactNode } from 'react';
import { NavLink } from 'react-router';
import styles from './AppShell.module.scss';
import type { Environment } from './environment';

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

const APP_GROUP: NavGroup = {
  id: 'app',
  items: [{ id: 'home', label: 'Início', icon: <Home01Icon fontSize="small" />, to: pathTo('home'), end: true }]
};

const ENGINEERING_GROUP: NavGroup = {
  id: 'engineering',
  label: 'Engenharia',
  items: [
    {
      id: 'project',
      label: 'Visão geral',
      icon: <BriefcaseIcon fontSize="small" />,
      to: pathTo('project'),
      end: true
    },
    { id: 'harness', label: 'Harness', icon: <ShieldCheckIcon fontSize="small" />, to: pathTo('projectHarness') },
    {
      id: 'knowledge',
      label: 'Conhecimento',
      icon: <BookOpenIcon fontSize="small" />,
      to: pathTo('projectKnowledge')
    },
    { id: 'configure', label: 'Configurar', icon: <ListViewRectangleIcon fontSize="small" />, to: pathTo('configure') },
    {
      id: 'request',
      label: 'Pedido de engenharia',
      icon: <AssignmentIcon fontSize="small" />,
      to: pathTo('engineeringRequest')
    },
    { id: 'catalog', label: 'Catálogo', icon: <LibraryIcon fontSize="small" />, unavailableReason: 'Em breve' }
  ]
};

const MANAGEMENT_GROUP: NavGroup = {
  id: 'management',
  label: 'Gestão',
  items: [
    { id: 'portfolio', label: 'Portfólio', icon: <GridIcon fontSize="small" />, to: pathTo('portfolio') },
    {
      id: 'management',
      label: 'Acompanhamento',
      icon: <ChartBarIcon fontSize="small" />,
      to: pathTo('management'),
      end: true
    },
    { id: 'assistant', label: 'Assistente', icon: <ChatIcon fontSize="small" />, to: pathTo('assistant') }
  ]
};

// Integrações alimenta os dois ambientes e por isso não pertence a nenhum: aparece sempre.
const PLATFORM_GROUP: NavGroup = {
  id: 'platform',
  label: 'Plataforma',
  items: [
    { id: 'runs', label: 'Execuções', icon: <PlayIcon fontSize="small" />, to: pathTo('runs') },
    {
      id: 'integrations',
      label: 'Integrações',
      icon: <IntersectingArrowsIcon fontSize="small" />,
      to: pathTo('integrations')
    },
    { id: 'settings', label: 'Configurações', icon: <SettingIcon fontSize="small" />, to: pathTo('settings') }
  ]
};

const ENVIRONMENT_GROUP: Record<Environment, NavGroup> = {
  engineering: ENGINEERING_GROUP,
  management: MANAGEMENT_GROUP
};

/** O menu mostra só o ambiente atual, mais o que é do aplicativo todo. */
export function SideNav({ environment }: { environment: Environment }) {
  const groups = [APP_GROUP, ENVIRONMENT_GROUP[environment], PLATFORM_GROUP];

  return (
    <nav aria-label="Navegação principal" className={styles.groups}>
      {groups.map((group) => (
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
