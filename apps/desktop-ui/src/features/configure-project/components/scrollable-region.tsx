import type { ReactNode } from 'react';
import styles from './panels.module.scss';

interface ScrollableRegionProps {
  label: string;
  children: ReactNode;
}

/** A região rolável precisa de foco para quem navega só pelo teclado. */
export function ScrollableRegion({ label, children }: ScrollableRegionProps) {
  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-tabindex
    <div role="region" aria-label={label} tabIndex={0} className={styles.scrollRegion}>
      {children}
    </div>
  );
}
