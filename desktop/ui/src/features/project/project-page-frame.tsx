import type { SelectedProject } from '@/features/project-selection';
import { NoProjectState, Page } from '@/shared/components';
import { Fragment, type ReactNode } from 'react';

interface ProjectPageFrameProps {
  project: SelectedProject | null;
  /** O título da tela enquanto não há projeto aberto. */
  title: string;
  children: (project: SelectedProject) => ReactNode;
}

/**
 * O que as telas do projeto têm em comum: sem projeto aberto, dizem onde escolher um; com projeto, remontam ao trocar de
 * projeto, para nada de um aparecer, nem por um instante, no outro.
 */
export function ProjectPageFrame({ project, title, children }: ProjectPageFrameProps) {
  if (!project) {
    return (
      <Page title={title}>
        <NoProjectState />
      </Page>
    );
  }
  return <Fragment key={project.projectId}>{children(project)}</Fragment>;
}
