import type { ReactNode } from 'react';
import styles from './PrimaryActionFooter.module.scss';

export function PrimaryActionFooter({ children }: { children: ReactNode }) {
  return (
    <div className={styles.footer} role="group" aria-label="Ações da etapa">
      {children}
    </div>
  );
}
