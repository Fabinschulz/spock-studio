import { Tooltip } from '@/shared/components';
import { cn } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useId } from 'react';
import { NavLink } from 'react-router';
import styles from './AppShell.module.scss';
import type { NavGroup, NavItem } from './navigation';
import { useRailCollapsed } from './rail-context';

export function NavGroupList({ group }: { group: NavGroup }) {
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

export function NavEntry({ item, className }: { item: NavItem; className?: string }) {
  const reasonId = useId();
  const collapsed = useRailCollapsed();

  if (item.to && !item.unavailableReason) {
    return (
      <Tooltip title={item.label} placement="right" disabled={!collapsed}>
        <NavLink to={item.to} end={item.end} className={cn(styles.item, className)}>
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
      </Tooltip>
    );
  }

  return (
    <Tooltip title={`${item.label}: ${item.unavailableReason}`} placement="right" disabled={!collapsed}>
      <Button
        variant="text"
        color="inherit"
        className={cn(styles.item, styles.unavailable, className)}
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
    </Tooltip>
  );
}
