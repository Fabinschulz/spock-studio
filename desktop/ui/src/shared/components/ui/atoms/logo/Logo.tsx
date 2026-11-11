import { cn } from '@/shared/utils';

import styles from './Logo.module.scss';

interface LogoProps {
  className?: string;
}

/**
 * Marca do Spock Studio, o olhar lógico: uma sobrancelha reta, a outra erguida em teal, e dois olhos. A mesma geometria
 * do ícone do aplicativo (`desktop/host/src-tauri/icons/source/spock-studio.svg`). No wordmark, o braço do "k" repete a
 * sobrancelha erguida. Decorativo.
 */
export function Logo({ className }: LogoProps) {
  return (
    <svg className={cn(styles.mark, className)} viewBox="0 0 32 32" aria-hidden="true" focusable="false">
      <rect className={styles.tile} width="32" height="32" rx="7" />
      <path className={styles.browFlat} d="M5.6 13.4H13" />
      <path className={styles.brow} d="M18.6 13.4L24.6 8.6L26.6 9.8" />
      <circle className={styles.eye} cx="9.3" cy="20" r="2.6" />
      <circle className={styles.eye} cx="22.4" cy="20" r="2.6" />
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
