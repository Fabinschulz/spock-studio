import { cn } from '@/shared/utils';

import styles from './Logo.module.scss';

interface LogoProps {
  className?: string;
}

/** A mesma geometria do ícone do aplicativo (`desktop/host/src-tauri/icons/source/spock-studio.svg`). Decorativo. */
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
