import styles from './tag.module.scss';
import type { TagProps } from './types';

export function Tag({ tone = 'neutral', children }: TagProps) {
  return (
    <span className={styles.tag} data-tone={tone}>
      {children}
    </span>
  );
}
