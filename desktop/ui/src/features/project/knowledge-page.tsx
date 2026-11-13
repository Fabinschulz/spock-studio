import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Page } from '@/shared/components';

import { KnowledgeSection } from './components/knowledge-section';
import { ProjectPageFrame } from './project-page-frame';

export function KnowledgePage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  return (
    <ProjectPageFrame project={project} title="Conhecimento">
      {(selected) => (
        <Page
          title="Conhecimento"
          description={`${selected.displayName} · o que os agentes podem ler do projeto e o que fica de fora.`}
        >
          <KnowledgeSection client={client} projectId={selected.projectId} />
        </Page>
      )}
    </ProjectPageFrame>
  );
}
