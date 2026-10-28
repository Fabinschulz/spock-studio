import type { ReactNode } from 'react';
import { Link } from 'react-router';

import styles from './PageHeader.module.scss';

export interface Crumb {
  label: string;
  /** Sem destino = página atual. */
  to?: string;
}

interface PageHeaderProps {
  title: ReactNode;
  description?: string;
  breadcrumb?: readonly Crumb[];
  /** Ações da página (à direita em janelas largas). */
  actions?: ReactNode;
}

/** Título da página (h1), trilha de navegação e texto de apoio. Uma por tela. */
export function PageHeader({ title, description, breadcrumb, actions }: PageHeaderProps) {
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
      <div className={styles.row}>
        <h1 className={styles.title}>{title}</h1>
        {actions ? <div className={styles.actions}>{actions}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
    </header>
  );
}
