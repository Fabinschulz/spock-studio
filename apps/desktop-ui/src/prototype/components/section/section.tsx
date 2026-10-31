import styles from './section.module.scss';
import type { SectionProps } from './types';

export function Section({ title, aside, children, id }: SectionProps) {
  const headingId = id ? `${id}-title` : undefined;

  return (
    <section className={styles.section} aria-labelledby={headingId}>
      <header className={styles.sectionHead}>
        <h2 id={headingId} className={styles.sectionTitle}>
          {title}
        </h2>
        {aside ? <div className={styles.sectionAside}>{aside}</div> : null}
      </header>
      {children}
    </section>
  );
}
