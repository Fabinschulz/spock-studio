import type { ReactNode } from 'react';
import styles from './panels.module.scss';

interface StepHeaderProps {
  title: string;
  children?: ReactNode;
}

export function StepHeader({ title, children }: StepHeaderProps) {
  return (
    <header className={styles.header}>
      {/* Alvo do foco ao trocar de etapa (useScrollToTop na jornada): fora da ordem do Tab. */}
      <h2 className={styles.title} tabIndex={-1} data-step-title="">
        {title}
      </h2>
      {children ? <p className={styles.intro}>{children}</p> : null}
    </header>
  );
}
