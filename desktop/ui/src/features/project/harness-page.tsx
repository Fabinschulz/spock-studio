import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Page } from '@/shared/components';

import { HarnessSection } from './components/harness-section';
import { RevisionsSection } from './components/revisions-section';
import { ProjectPageFrame } from './project-page-frame';

/** O Harness aplicado no projeto, conferido contra o disco, e a história de cada mudança que ele teve. */
export function HarnessPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  return (
    <ProjectPageFrame project={project} title="Harness">
      {(selected) => (
        <Page
          title="Harness"
          description={`${selected.displayName} · o que o Spock Studio aplicou no projeto e como isso mudou.`}
        >
          <HarnessSection client={client} projectId={selected.projectId} />
          <RevisionsSection client={client} projectId={selected.projectId} />
        </Page>
      )}
    </ProjectPageFrame>
  );
}
