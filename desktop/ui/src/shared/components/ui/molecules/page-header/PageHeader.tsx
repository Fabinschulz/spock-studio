import type { ReactNode } from 'react';
import styles from './PageHeader.module.scss';
import { TrailNav } from './trail-nav';

export interface Crumb {
  label: string;
  to?: string;
}

interface PageHeaderProps {
  title: ReactNode;
  description?: ReactNode;
  breadcrumb?: readonly Crumb[];
  actions?: ReactNode;
}

export function PageHeader({ title, description, breadcrumb, actions }: PageHeaderProps) {
  return (
    <header className={styles.header}>
      <TrailNav trail={breadcrumb ?? []} />
      <div className={styles.row}>
        <h1 className={styles.title}>{title}</h1>
        {actions ? <div className={styles.actions}>{actions}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
    </header>
  );
}
