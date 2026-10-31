import styles from './icon-button.module.scss';
import type { IconButtonProps } from './types';

export function IconButton({ children, ...rest }: IconButtonProps) {
  return (
    <button type="button" className={styles.iconButton} {...rest}>
      {children}
    </button>
  );
}
