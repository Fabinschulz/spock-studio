import { useId } from 'react';
import { formatRecentDate } from '../domain/format-recent-date';
import type { RecentProject } from '../domain/types';
import { RecentProjectButton } from './recent-project-button';
import styles from './recent-projects.module.scss';

const classes = {
  button: styles.row,
  icon: styles.icon,
  text: styles.text,
  name: styles.name,
  path: styles.path,
  meta: styles.meta
};

interface RecentProjectsProps {
  recents: readonly RecentProject[];
  /** Id do recente que corresponde ao projeto aberto. */
  currentId?: string;
  busy?: boolean;
  onOpen: (id: string) => void;
}

export function RecentProjects({ recents, currentId, busy, onOpen }: RecentProjectsProps) {
  const id = useId();
  if (recents.length === 0) return null;

  return (
    <section className={styles.recents} aria-labelledby={`${id}-titulo`}>
      <h3 id={`${id}-titulo`} className={styles.title}>
        Projetos recentes
      </h3>
      <ul className={styles.list} role="list">
        {recents.map((project) => (
          <li key={project.id} className={styles.item}>
            <RecentProjectButton
              project={project}
              current={project.id === currentId}
              busy={busy}
              formatDate={formatRecentDate}
              classes={classes}
              onOpen={onOpen}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}
