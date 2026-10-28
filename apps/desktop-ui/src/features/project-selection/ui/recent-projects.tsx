import { useId } from 'react';

import { FolderIcon } from '@/shared/components/ui';

import { formatRecentDate } from '../core/format-recent-date';
import type { RecentProject } from '../core/types';
import styles from './recent-projects.module.scss';

interface RecentProjectsProps {
  recents: readonly RecentProject[];
  /** Nome do projeto escolhido agora, para marcá-lo. */
  selectedName?: string;
  busy?: boolean;
  onOpen: (id: string) => void;
}

/**
 * Projetos escolhidos antes. Escolher um aqui só o seleciona, como o diálogo: analisar é uma ação à parte.
 * Pasta que não existe mais fica visível, mas indisponível e com o motivo dito.
 */
export function RecentProjects({ recents, selectedName, busy, onOpen }: RecentProjectsProps) {
  const id = useId();
  if (recents.length === 0) return null;

  return (
    <section className={styles.recents} aria-labelledby={`${id}-titulo`}>
      <h2 id={`${id}-titulo`} className={styles.title}>
        Projetos recentes
      </h2>
      <ul className={styles.list} role="list">
        {recents.map((project) => {
          const inactive = busy || !project.available;
          const reasonId = `${id}-${project.id}-motivo`;
          return (
            <li key={project.id} className={styles.item}>
              <button
                type="button"
                className={styles.card}
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
                  <span id={reasonId} className={styles.meta}>
                    {project.available
                      ? formatRecentDate(project.lastOpenedMs)
                      : 'Pasta indisponível: não existe mais ou não está acessível'}
                  </span>
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
