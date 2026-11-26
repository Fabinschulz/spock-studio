import { Button } from '@mariliz/ui-kit';

import { useProjectSelectionContext } from '@/features/project-selection';

import styles from '../../components/panels.module.scss';
import type { PickedProject } from './folder-picker-card';

const folderOf = (path: string): string => path.split(/[\\/]/).filter(Boolean).at(-1) ?? path;

/**
 * Com projeto aberto, escolher de novo é pelo seletor do topo: aqui não se repete o campo de pasta nem os recentes. O
 * nome é o que o projeto declara (o mesmo do topo e do resumo); o caminho só diz onde ele está.
 */
export function ProjectLine({ project }: { project: PickedProject }) {
  const { setSwitcherOpen } = useProjectSelectionContext();
  const folder = folderOf(project.path);
  const renamed = folder.toLowerCase() !== project.name.toLowerCase();

  return (
    <div className={styles.projectLine} role="group" aria-label="Projeto">
      <span className={styles.muted}>Projeto</span>
      <span className={styles.projectName}>{project.name}</span>
      <span className={styles.projectPath} title={project.path}>
        na pasta {project.path}
      </span>
      <Button
        variant="text"
        size="small"
        onClick={() => setSwitcherOpen(true)}
        aria-label={`Trocar o projeto ${project.name}`}
      >
        Trocar
      </Button>
      {renamed ? (
        <p className={styles.projectNote}>
          O nome vem do próprio projeto (do package.json, por exemplo), não do nome da pasta, que é {folder}.
        </p>
      ) : null}
    </div>
  );
}
