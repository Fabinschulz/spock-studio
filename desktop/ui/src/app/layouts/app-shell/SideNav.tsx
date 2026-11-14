import type { RunActivity } from '@/features/runs';
import { cn } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useId } from 'react';
import { NavLink } from 'react-router';
import styles from './AppShell.module.scss';
import type { Environment } from './environment';
import { APP_GROUP, ENVIRONMENT_GROUP, PLATFORM_GROUP, type NavGroup, type NavItem } from './navigation';

export type { NavItem };
export function SideNav({
  environment,
  runActivity = null
}: {
  environment: Environment;
  runActivity?: RunActivity | null;
}) {
  const groups = [APP_GROUP, ENVIRONMENT_GROUP[environment], withRunActivity(PLATFORM_GROUP, runActivity)];

  return (
    <nav aria-label="Navegação principal" className={styles.groups}>
      {groups.map((group) => (
        <NavGroupList key={group.id} group={group} />
      ))}
    </nav>
  );
}

/**
 * O que espera a pessoa vem antes do que só está andando: com aprovação pendente o aviso mostra esse número, em tom de
 * atenção; senão, o de execuções em andamento. O nome acessível do link diz os dois.
 */
export function describeRunActivity(activity: RunActivity | null): NavItem['activity'] {
  if (!activity) return undefined;
  const { running, awaitingApproval } = activity;
  if (running === 0 && awaitingApproval === 0) return undefined;

  const label = [
    awaitingApproval > 0 ? `${awaitingApproval} aguardando aprovação` : null,
    running > 0 ? `${running} em andamento` : null
  ]
    .filter(Boolean)
    .join(', ');
  return awaitingApproval > 0
    ? { text: String(awaitingApproval), label, tone: 'warning' }
    : { text: String(running), label, tone: 'info' };
}

function withRunActivity(group: NavGroup, runActivity: RunActivity | null): NavGroup {
  const activity = describeRunActivity(runActivity);
  if (!activity) return group;
  return { ...group, items: group.items.map((item) => (item.id === 'runs' ? { ...item, activity } : item)) };
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

export function NavEntry({ item }: { item: NavItem }) {
  const reasonId = useId();

  if (item.to && !item.unavailableReason) {
    return (
      <NavLink to={item.to} end={item.end} className={({ isActive }) => cn(styles.item, isActive && styles.active)}>
        <span className={styles.itemIcon} aria-hidden="true">
          {item.icon}
        </span>
        <span className={styles.itemText}>
          <span className={styles.itemLabel}>{item.label}</span>
          {item.activity ? (
            <span className={cn(styles.activity, styles[item.activity.tone])}>
              <span aria-hidden="true">{item.activity.text}</span>
              <span className="visually-hidden">, {item.activity.label}</span>
            </span>
          ) : null}
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
