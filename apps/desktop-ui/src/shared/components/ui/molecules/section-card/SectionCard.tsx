import { useId, type ReactNode } from 'react';

import { cn } from '@/shared/utils';

import styles from './SectionCard.module.scss';

interface SectionCardProps {
  title: string;
  /** Selo ou ação ao lado do título (ex.: estado da instalação). */
  aside?: ReactNode;
  children: ReactNode;
  /** `accent` destaca o cartão principal da tela; o resto da interface usa `default`. */
  tone?: 'default' | 'accent';
  className?: string;
}

/** Bloco de informação com título próprio: a unidade de leitura das etapas. */
export function SectionCard({ title, aside, children, tone = 'default', className }: SectionCardProps) {
  const id = useId();
  return (
    <section className={cn(styles.card, tone === 'accent' && styles.accent, className)} aria-labelledby={id}>
      <header className={styles.header}>
        <h3 id={id} className={styles.title}>
          {title}
        </h3>
        {aside ? <div className={styles.aside}>{aside}</div> : null}
      </header>
      <div className={styles.body}>{children}</div>
    </section>
  );
}
