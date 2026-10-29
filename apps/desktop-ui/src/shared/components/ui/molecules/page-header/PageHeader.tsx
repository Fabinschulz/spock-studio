import type { ReactNode } from 'react';
import { Link } from 'react-router';

import styles from './PageHeader.module.scss';

export interface Crumb {
  label: string;
  to?: string;
}

interface PageHeaderProps {
  title: ReactNode;
  description?: string;
  breadcrumb?: readonly Crumb[];
  actions?: ReactNode;
  lead?: ReactNode;
}

export function PageHeader({ title, description, breadcrumb, actions, lead }: PageHeaderProps) {
  return (
    <header className={styles.header}>
      {breadcrumb && breadcrumb.length > 0 ? (
        <nav aria-label="Trilha de navegação">
          <ol className={styles.crumbs}>
            {breadcrumb.map((crumb, index) => (
              <li key={crumb.label} className={styles.crumb}>
                {crumb.to ? (
                  <Link to={crumb.to} className={styles.link}>
                    {crumb.label}
                  </Link>
                ) : (
                  <span aria-current={index === breadcrumb.length - 1 ? 'page' : undefined}>{crumb.label}</span>
                )}
              </li>
            ))}
          </ol>
        </nav>
      ) : null}
      {lead}
      <div className={styles.row}>
        <h1 className={styles.title}>{title}</h1>
        {actions ? <div className={styles.actions}>{actions}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
    </header>
  );
}
