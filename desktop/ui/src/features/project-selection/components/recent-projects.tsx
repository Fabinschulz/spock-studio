import { FolderIcon } from '@/shared/components/ui';
import { useId } from 'react';
import { formatRecentDate } from '../domain/format-recent-date';
import type { RecentProject } from '../domain/types';
import styles from './recent-projects.module.scss';

interface RecentProjectsProps {
  recents: readonly RecentProject[];
  selectedName?: string;
  busy?: boolean;
  onOpen: (id: string) => void;
}

export function RecentProjects({ recents, selectedName, busy, onOpen }: RecentProjectsProps) {
  const id = useId();
  if (recents.length === 0) return null;

  return (
    <section className={styles.recents} aria-labelledby={`${id}-titulo`}>
      <h3 id={`${id}-titulo`} className={styles.title}>
        Projetos recentes
      </h3>
      <ul className={styles.list} role="list">
        {recents.map((project) => {
          const inactive = busy || !project.available;
          const reasonId = `${id}-${project.id}-motivo`;
          return (
            <li key={project.id} className={styles.item}>
              <button
                type="button"
                className={styles.row}
                aria-disabled={inactive || undefined}
                aria-current={project.displayName === selectedName ? 'true' : undefined}
                aria-describedby={reasonId}
                onClick={inactive ? undefined : () => onOpen(project.id)}
              >
                <span className={styles.icon} aria-hidden="true">
                  <FolderIcon size={20} />
                </span>
                <span className={styles.text}>
                  <span className={styles.name}>{project.displayName}</span>
                  <span className={styles.path} title={project.displayPath}>
                    {project.displayPath}
                  </span>
                </span>
                <span id={reasonId} className={styles.meta}>
                  {project.available
                    ? formatRecentDate(project.lastOpenedMs)
                    : 'Indisponível: a pasta não existe mais ou não está acessível'}
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
