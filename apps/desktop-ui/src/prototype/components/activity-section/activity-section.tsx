import { Section } from '@/prototype/components/section';
import { formatClock } from '@/prototype/domain/format-clock';

import styles from './activity-section.module.scss';
import type { ActivitySectionProps } from './types';

const VISIBLE_ITEMS = 7;

export function ActivitySection({ items, live }: ActivitySectionProps) {
  const recent = items.slice(-VISIBLE_ITEMS).reverse();
  const proposed = items.some((item) => item.proposed);
  const aside = proposed ? 'demonstração' : live ? 'ao vivo' : undefined;

  return (
    <Section title="Atividade" id="atividade" aside={aside}>
      {recent.length === 0 ? <p className={styles.empty}>Nenhum evento ainda.</p> : null}
      <ol role="list" className={styles.feed}>
        {recent.map((item) => (
          <li key={item.key} className={styles.feedItem}>
            <time className={styles.feedTime}>{formatClock(item.at)}</time>
            <span className={styles.feedText}>{item.text}</span>
          </li>
        ))}
      </ol>
    </Section>
  );
}
