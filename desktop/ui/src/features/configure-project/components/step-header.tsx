import type { ReactNode } from 'react';
import styles from './panels.module.scss';

interface StepHeaderProps {
  title: string;
  children?: ReactNode;
}

export function StepHeader({ title, children }: StepHeaderProps) {
  return (
    <header className={styles.header}>
      <h2 className={styles.title}>{title}</h2>
      {children ? <p className={styles.intro}>{children}</p> : null}
    </header>
  );
}
