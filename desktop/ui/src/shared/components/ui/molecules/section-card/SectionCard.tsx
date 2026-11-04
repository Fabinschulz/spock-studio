import { cn } from '@/shared/utils';
import { useId, type ReactNode } from 'react';
import styles from './SectionCard.module.scss';

interface SectionCardProps {
  title: string;
  icon?: ReactNode;
  aside?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function SectionCard({ title, icon, aside, children, className }: SectionCardProps) {
  const id = useId();
  return (
    <section className={cn(styles.card, className)} aria-labelledby={id}>
      <header className={styles.header}>
        <div className={styles.heading}>
          {icon ? (
            <span className={styles.icon} aria-hidden="true">
              {icon}
            </span>
          ) : null}
          <h3 id={id} className={styles.title}>
            {title}
          </h3>
        </div>
        {aside ? <div className={styles.aside}>{aside}</div> : null}
      </header>
      <div className={styles.body}>{children}</div>
    </section>
  );
}
