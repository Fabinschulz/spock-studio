import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, InlineNotice, LoadingState, Page } from '@/shared/components';
import { useCallback, useMemo } from 'react';

import { ConnectionsSection } from './components/connections-section';
import { ProjectIntegrationsSection } from './components/project-integrations-section';
import { useReloadable } from './hooks/use-reloadable';
import { createIntegrationApi } from './service/integration-api';

/** Central de integrações: as conexões do computador e, para o projeto aberto, o que ele pode ler de cada uma. */
export function IntegrationsPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIntegrationApi(client), [client]);
  const load = useCallback(async () => {
    // O resumo é um complemento: se ele falhar, a central continua inteira e cada conexão diz que não conseguiu mostrá-lo.
    const [catalog, connections, reach] = await Promise.all([
      api.catalog(),
      api.connections(),
      api.reach().catch(() => null)
    ]);
    return { catalog, connections, reach };
  }, [api]);
  const hub = useReloadable(load);

  return (
    <Page
      title="Integrações"
      description="Conecte as ferramentas da equipe e decida, projeto a projeto, o que o Spock Studio pode ler. As credenciais ficam protegidas neste computador e não aparecem em nenhuma tela."
    >
      {!hub.settled ? <LoadingState>Lendo as integrações…</LoadingState> : null}
      {hub.settled && hub.value === null ? (
        <ErrorState onRetry={hub.reload}>{errorMessage(hub.error, 'Não foi possível ler as integrações.')}</ErrorState>
      ) : null}

      {hub.value ? (
        <>
          {!hub.value.catalog.vaultAvailable ? (
            <InlineNotice tone="warning" title="Armazenamento protegido indisponível">
              Neste sistema o Spock Studio ainda não tem onde guardar credenciais com proteção do sistema operacional.
              Para não gravá-las em texto puro, novas conexões ficam desativadas.
            </InlineNotice>
          ) : null}
          <ConnectionsSection
            api={api}
            catalog={hub.value.catalog}
            connections={hub.value.connections}
            reach={hub.value.reach}
            onChanged={hub.reload}
          />
          <ProjectIntegrationsSection
            api={api}
            project={project}
            catalog={hub.value.catalog}
            connections={hub.value.connections}
            onChanged={hub.reload}
          />
        </>
      ) : null}
    </Page>
  );
}
