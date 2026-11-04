import { useId } from 'react';
import { useProjectSelectionContext } from '../context/project-selection-context';
import { formatRecentDateTime } from '../domain/format-recent-date';
import { RecentProjectButton } from './recent-project-button';
import styles from './recent-shortcuts.module.scss';

const classes = {
  button: styles.card,
  icon: styles.icon,
  text: styles.text,
  name: styles.name,
  path: styles.path,
  meta: styles.meta
};

export function RecentShortcuts() {
  const id = useId();
  const { recents, selected, busy, openRecent } = useProjectSelectionContext();
  if (recents.length === 0) return null;

  return (
    <section className={styles.section} aria-labelledby={`${id}-titulo`}>
      <h2 id={`${id}-titulo`} className={styles.title}>
        Projetos recentes
      </h2>
      <ul className={styles.grid} role="list">
        {recents.map((project) => (
          <li key={project.id} className={styles.item}>
            <RecentProjectButton
              project={project}
              current={project.id === selected?.recentId}
              busy={busy}
              formatDate={formatRecentDateTime}
              classes={classes}
              onOpen={(recentId) => void openRecent(recentId)}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}
