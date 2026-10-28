import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './SectionCard.module.scss';

interface SectionCardProps {
  title: string;
  /** Ícone decorativo antes do título (o nome do cartão continua sendo o título). */
  icon?: ReactNode;
  /** Selo ou ação ao lado do título (ex.: estado da instalação). */
  aside?: ReactNode;
  children: ReactNode;
  /** `accent` destaca o cartão principal da tela; o resto da interface usa `default`. */
  tone?: 'default' | 'accent';
  className?: string;
}

/** Bloco de informação com título próprio: a unidade de leitura das etapas. */
export function SectionCard({ title, icon, aside, children, tone = 'default', className }: SectionCardProps) {
  const id = useId();
  return (
    <section className={cn(styles.card, tone === 'accent' && styles.accent, className)} aria-labelledby={id}>
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
