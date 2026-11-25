import { cn } from '@/shared/utils';
import { Button, CountBadge } from '@mariliz/ui-kit';
import { useId } from 'react';
import { Link, NavLink, useLocation } from 'react-router';
import styles from './AppShell.module.scss';
import type { NavItem } from './navigation';

export function NavEntry({ item, className }: { item: NavItem; className?: string }) {
  const reasonId = useId();
  const { pathname } = useLocation();

  if (item.to && !item.unavailableReason) {
    const content = (
      <>
        <span className={styles.itemIcon} aria-hidden="true">
          {item.icon}
        </span>
        <span className={styles.itemText}>
          <span className={styles.itemLabel}>{item.label}</span>
          {item.activity ? (
            <CountBadge count={item.activity.text} label={item.activity.label} tone={item.activity.tone} />
          ) : null}
        </span>
      </>
    );
    // Um item com abas fica marcado em qualquer uma delas; o NavLink só reconheceria a própria rota.
    return item.tabs ? (
      <Link
        to={item.to}
        className={cn(styles.item, className)}
        aria-current={item.tabs.some((tab) => tab.to === pathname) ? 'page' : undefined}
      >
        {content}
      </Link>
    ) : (
      <NavLink to={item.to} end={item.end} className={cn(styles.item, className)}>
        {content}
      </NavLink>
    );
  }

  return (
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
  );
}
