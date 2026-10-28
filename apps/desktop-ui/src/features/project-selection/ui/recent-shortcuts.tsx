import { useId } from 'react';

import { FolderIcon } from '@/shared/components/ui';

import { useProjectSelectionContext } from '../context/project-selection-context';
import { formatRecentDateTime } from '../core/format-recent-date';
import styles from './recent-shortcuts.module.scss';

/**
 * Atalhos para os projetos escolhidos antes, na tela inicial: nome, caminho abreviado e o último acesso.
 * Escolher um só o seleciona, como o diálogo; a análise começa pela atividade "Configurar projeto".
 */
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
        {recents.map((project) => {
          const inactive = busy || !project.available;
          const reasonId = `${id}-${project.id}-motivo`;
          return (
            <li key={project.id} className={styles.item}>
              <button
                type="button"
                className={styles.card}
                aria-disabled={inactive || undefined}
                aria-current={project.displayName === selected?.displayName ? 'true' : undefined}
                aria-describedby={reasonId}
                onClick={inactive ? undefined : () => void openRecent(project.id)}
              >
                <span className={styles.icon} aria-hidden="true">
                  <FolderIcon size={20} />
                </span>
                <span className={styles.text}>
                  <span className={styles.name}>{project.displayName}</span>
                  <span className={styles.path} title={project.displayPath}>
                    {project.displayPath}
                  </span>
                  <span id={reasonId} className={styles.meta}>
                    {project.available
                      ? formatRecentDateTime(project.lastOpenedMs)
                      : 'Indisponível: a pasta não existe mais ou não está acessível'}
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
