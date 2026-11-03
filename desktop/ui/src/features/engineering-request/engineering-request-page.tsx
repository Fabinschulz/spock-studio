import { ProjectSelection, useProjectSelectionContext, type SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';

import { EngineeringRequestForm } from './components/engineering-request-form';
import { EngineeringRequestLayout } from './components/engineering-request-layout';
import { ProjectContextBanner } from './components/project-context-banner';
import { useStartEngineeringRequest } from './hooks/use-start-engineering-request';

interface EngineeringRequestPageProps {
  client: EngineClient;
}

const DESCRIPTION_WITH_PROJECT =
  'Stacks e evidências vêm da descoberta do projeto; nada é gravado no repositório nesta fase.';
const DESCRIPTION_WITHOUT_PROJECT =
  'Descreva o que precisa analisar. O motor executa fora do repositório até você aprovar na tela de execução.';

export function EngineeringRequestPage({ client }: EngineeringRequestPageProps) {
  const { selected } = useProjectSelectionContext();

  if (!selected) {
    return (
      <EngineeringRequestLayout description={DESCRIPTION_WITHOUT_PROJECT}>
        <ProjectSelection />
      </EngineeringRequestLayout>
    );
  }

  return <EngineeringRequestPageWithProject client={client} projectId={selected.projectId} project={selected} />;
}

function EngineeringRequestPageWithProject({
  client,
  projectId,
  project
}: {
  client: EngineClient;
  projectId: string;
  project: SelectedProject;
}) {
  const { submit, busy, error } = useStartEngineeringRequest(client, projectId);

  return (
    <EngineeringRequestLayout description={DESCRIPTION_WITH_PROJECT}>
      <ProjectContextBanner project={project} />
      <EngineeringRequestForm busy={busy} error={error} onSubmit={submit} />
    </EngineeringRequestLayout>
  );
}
