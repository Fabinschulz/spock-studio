import { Button } from '@mariliz/ui-kit';

import { useProjectSelectionContext } from '@/features/project-selection';

import styles from '../../components/panels.module.scss';
import type { PickedProject } from './folder-picker-card';

/** Com projeto aberto, escolher de novo é pelo seletor do topo: aqui não se repete o campo de pasta nem os recentes. */
export function ProjectLine({ project }: { project: PickedProject }) {
  const { setSwitcherOpen } = useProjectSelectionContext();

  return (
    <div className={styles.projectLine} role="group" aria-label="Projeto">
      <span className={styles.muted}>Projeto</span>
      <span className={styles.projectName}>{project.name}</span>
      <span className={styles.projectPath} title={project.path}>
        {project.path}
      </span>
      <Button
        variant="text"
        size="small"
        onClick={() => setSwitcherOpen(true)}
        aria-label={`Trocar o projeto ${project.name}`}
      >
        Trocar
      </Button>
    </div>
  );
}
