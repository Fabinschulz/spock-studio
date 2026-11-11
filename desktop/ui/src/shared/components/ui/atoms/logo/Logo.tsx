import { cn } from '@/shared/utils';

import styles from './Logo.module.scss';

interface LogoProps {
  className?: string;
}

/**
 * Marca do Spock Studio: um "S" em trilha de circuito que termina num terminal em anel (que também se lê como um olho)
 * e, sobre ele, uma sobrancelha erguida em teal. A mesma geometria do ícone do aplicativo
 * (`desktop/host/src-tauri/icons/source/spock-studio.svg`). Decorativo.
 */
export function Logo({ className }: LogoProps) {
  return (
    <svg className={cn(styles.mark, className)} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <rect className={styles.tile} width="32" height="32" rx="7" />
      <path className={styles.letter} d="M18.2 12H12.5L9.5 15V16.5L12.5 19.5H17.5L20.5 22.5V24L17.5 27H9.5" />
      <circle className={styles.eye} cx="20.6" cy="12" r="2.5" />
      <path className={styles.brow} d="M16.6 7.4L22.4 4.6L24.6 5.6" />
    </svg>
  );
}

/** Wordmark "spock" monolinha; o braço do "k" é a sobrancelha erguida da marca, em teal. Decorativo. */
export function Wordmark({ className }: LogoProps) {
  return (
    <svg className={cn(styles.wordmark, className)} viewBox="-2.5 -2.5 123 42" aria-hidden="true" focusable="false">
      <path
        className={styles.letters}
        d="M11.33 10.5A5 5 0 1 0 7 18A5 5 0 1 1 2.67 25.5M22 8V37M94.43 10.34A10 10 0 1 0 94.43 25.66M104 0V28M110.5 16.5L118 28"
      />
      <circle className={styles.letters} cx="32" cy="18" r="10" />
      <circle className={styles.letters} cx="60" cy="18" r="10" />
      <path className={styles.arm} d="M105 20.5L117 10" />
    </svg>
  );
}
