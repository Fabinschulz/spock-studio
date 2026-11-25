import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Page } from '@/shared/components';

import { HarnessSection } from './components/harness-section';
import { RevisionsSection } from './components/revisions-section';
import { ProjectPageFrame } from './project-page-frame';

export function HarnessPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  return (
    <ProjectPageFrame project={project} title="Padrões do projeto">
      {(selected) => (
        <Page
          title="Padrões do projeto"
          description={`${selected.displayName} · o Harness do projeto: as regras, as verificações e os assistentes de IA que o Spock Studio aplicou, e como isso mudou.`}
        >
          <HarnessSection client={client} projectId={selected.projectId} />
          <RevisionsSection client={client} projectId={selected.projectId} />
        </Page>
      )}
    </ProjectPageFrame>
  );
}
