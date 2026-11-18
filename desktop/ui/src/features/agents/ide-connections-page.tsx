import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { Page } from '@/shared/components';
import { useMemo } from 'react';

import { IdeConnectionSection } from './components/ide-connection-section';
import { createAgentApi } from './service/agent-api';

/** A IDE é uma conexão como as outras: o que ela lê do Spock se configura em Conexões, não na tela de pedidos. */
export function IdeConnectionsPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createAgentApi(client), [client]);
  return (
    <Page
      title="IDEs"
      description="Ligue o Spock à ferramenta em que você programa: ela passa a ler o contexto do projeto pelo servidor MCP do Spock, só para leitura."
    >
      <IdeConnectionSection api={api} project={project} />
    </Page>
  );
}
