import { cn } from '@/shared/utils';
import type { ReactNode } from 'react';
import styles from './Disclosure.module.scss';

interface DisclosureProps {
  summary: ReactNode;
  /** Abre já na primeira vez que aparece. Depois, quem decide é a pessoa. */
  defaultOpen?: boolean;
  className?: string;
  children: ReactNode;
}

/** É o `<details>` do navegador: teclado, leitor de tela e "aberto ou fechado" já funcionam sem código. */
export function Disclosure({ summary, defaultOpen, className, children }: DisclosureProps) {
  return (
    <details className={cn(styles.details, className)} open={defaultOpen || undefined}>
      <summary className={styles.summary}>{summary}</summary>
      <div className={styles.content}>{children}</div>
    </details>
  );
}
