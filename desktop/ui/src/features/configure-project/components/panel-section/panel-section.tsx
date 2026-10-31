import styles from './panel-section.module.scss';
import type { PanelSectionProps } from './types';

export function PanelSection({ id, title, children }: PanelSectionProps) {
  return (
    <section className={styles.section} aria-labelledby={id}>
      <h3 id={id} className={styles.title}>
        {title}
      </h3>
      {children}
    </section>
  );
}
