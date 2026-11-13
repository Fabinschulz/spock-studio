import { cn } from '@/shared/utils';
import { useId, type ReactNode } from 'react';
import styles from './PageSection.module.scss';

interface PageSectionProps {
  title: ReactNode;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
  children?: ReactNode;
}

/** O nome acessível da região é o título, ligado por um id que o componente cria: ninguém repete um id à mão. */
export function PageSection({ title, description, action, className, children }: PageSectionProps) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} className={cn(styles.section, className)}>
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {action ? <div className={styles.action}>{action}</div> : null}
      </div>
      {description ? <p className={styles.description}>{description}</p> : null}
      {children}
    </section>
  );
}
