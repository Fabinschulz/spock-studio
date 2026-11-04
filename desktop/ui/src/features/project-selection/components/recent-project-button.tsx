import { FolderIcon } from '@/shared/components/ui';
import { useId } from 'react';
import type { RecentProject } from '../domain/types';

const UNAVAILABLE = 'Indisponível: a pasta não existe mais ou não está acessível';

/** Classes de cada parte: a lista e os atalhos têm o mesmo comportamento com aparências diferentes. */
export type RecentProjectClasses = Record<'button' | 'icon' | 'text' | 'name' | 'path' | 'meta', string | undefined>;

interface RecentProjectButtonProps {
  project: RecentProject;
  /** O projeto aberto agora, comparado pelo id do recente (nomes podem se repetir entre pastas). */
  current: boolean;
  busy?: boolean;
  formatDate: (ms: number) => string;
  classes: RecentProjectClasses;
  onOpen: (id: string) => void;
}

/** Um projeto recente: o mesmo comportamento na lista da configuração e nos atalhos do início. */
export function RecentProjectButton({ project, current, busy, formatDate, classes, onOpen }: RecentProjectButtonProps) {
  const reasonId = useId();
  const inactive = busy || !project.available;

  return (
    <button
      type="button"
      className={classes.button}
      aria-disabled={inactive || undefined}
      aria-current={current ? 'true' : undefined}
      aria-describedby={reasonId}
      onClick={inactive ? undefined : () => onOpen(project.id)}
    >
      <span className={classes.icon} aria-hidden="true">
        <FolderIcon size={20} />
      </span>
      <span className={classes.text}>
        <span className={classes.name}>{project.displayName}</span>
        <span className={classes.path} title={project.displayPath}>
          {project.displayPath}
        </span>
      </span>
      <span id={reasonId} className={classes.meta}>
        {project.available ? formatDate(project.lastOpenedMs) : UNAVAILABLE}
      </span>
    </button>
  );
}
