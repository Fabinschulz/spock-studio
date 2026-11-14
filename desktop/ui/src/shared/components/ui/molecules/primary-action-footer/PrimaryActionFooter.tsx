import type { ReactNode } from 'react';
import styles from './PrimaryActionFooter.module.scss';

/** `hint` diz por que a ação principal ainda não está disponível e o que falta fazer. */
export function PrimaryActionFooter({ hint, children }: { hint?: ReactNode; children: ReactNode }) {
  return (
    <div className={styles.footer} role="group" aria-label="Ações da etapa">
      {hint ? <p className={styles.hint}>{hint}</p> : null}
      {children}
    </div>
  );
}
