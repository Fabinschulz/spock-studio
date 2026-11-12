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

/**
 * O "ver detalhes" do produto: o que a pessoa lê quando quer, atrás de um título que diz o que há ali. É o `<details>` do
 * navegador, então teclado, leitor de tela e "aberto ou fechado" já funcionam sem código; o componente só dá a todos o
 * mesmo visual, em vez de cada tela refazer o seu.
 */
export function Disclosure({ summary, defaultOpen, className, children }: DisclosureProps) {
  return (
    <details className={cn(styles.details, className)} open={defaultOpen || undefined}>
      <summary className={styles.summary}>{summary}</summary>
      <div className={styles.content}>{children}</div>
    </details>
  );
}
