import styles from './fact.module.scss';
import type { FactProps } from './types';

export function Fact({ label, children }: FactProps) {
  return (
    <div className={styles.fact}>
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}
