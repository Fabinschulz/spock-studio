import { formatElapsed } from '@/shared/utils';
import { PanelSection } from '../panel-section';
import styles from './activity-section.module.scss';
import type { ActivitySectionProps } from './types';

const VISIBLE_EVENTS = 7;

export function ActivitySection({ feed, startedAt }: ActivitySectionProps) {
  const recent = feed.slice(-VISIBLE_EVENTS).reverse();

  return (
    <PanelSection id="ctx-atividade" title="Atividade">
      {recent.length === 0 ? <p className={styles.empty}>Nenhum evento ainda.</p> : null}
      <ol className={styles.feed}>
        {recent.map((item, index) => (
          <li key={`${item.at}-${index}`} className={styles.item}>
            <time className={styles.time}>{formatElapsed(item.at - startedAt)}</time>
            <span className={styles.text}>{item.text}</span>
          </li>
        ))}
      </ol>
    </PanelSection>
  );
}
