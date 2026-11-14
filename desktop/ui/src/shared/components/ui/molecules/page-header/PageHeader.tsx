import { Breadcrumb } from '@mariliz/ui-kit';
import type { ReactNode } from 'react';
import styles from './PageHeader.module.scss';

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
      {breadcrumb && breadcrumb.length > 0 ? (
        <Breadcrumb
          aria-label="Trilha de navegação"
          links={breadcrumb.map((crumb) => ({ url: crumb.to ?? '', title: crumb.label }))}
        />
      ) : null}
      <div className={styles.row}>
        <h1 className={styles.title}>{title}</h1>
        {actions ? <div className={styles.actions}>{actions}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
    </header>
  );
}
