import { cn } from '@/shared/utils';

import styles from './Logo.module.scss';

interface LogoProps {
  className?: string;
}

/** Marca do aplicativo. Decorativa: quem usa fornece o nome acessível. */
export function Logo({ className }: LogoProps) {
  return (
    <svg
      className={cn(styles.logo, className)}
      width="36"
      height="36"
      viewBox="0 0 36 36"
      aria-hidden="true"
      focusable="false"
    >
      <rect className={styles.tile} width="36" height="36" rx="9" />
      <path className={styles.letter} d="M9 26V10l9 10 9-10v16" />
    </svg>
  );
}
