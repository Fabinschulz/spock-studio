import { useReloadable } from '@/shared/hooks';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { ErrorState, InlineNotice, LoadingState, Page } from '@/shared/components';
import { useCallback, useMemo } from 'react';

import { ConnectionsSection } from './components/connections-section';
import { ProjectIntegrationsSection } from './components/project-integrations-section';
import styles from './integrations-page.module.scss';
import { createIntegrationApi } from './service/integration-api';

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
      title="Serviços"
      description="Conecte as ferramentas da equipe, como GitHub e Jira, e decida, projeto a projeto, o que o Spock Studio pode ler. As credenciais ficam protegidas neste computador e não aparecem em nenhuma tela."
    >
      {!hub.settled ? <LoadingState>Lendo as integrações…</LoadingState> : null}
      {hub.settled && hub.value === null ? (
        <ErrorState onRetry={hub.reload}>{errorMessage(hub.error, 'Não foi possível ler as integrações.')}</ErrorState>
      ) : null}

      {hub.value ? (
        <>
          {!hub.value.catalog.vaultAvailable ? (
            <InlineNotice tone="warning" title="Armazenamento protegido indisponível">
              O Spock Studio não encontrou um cofre do sistema para guardar credenciais com proteção. Para não gravá-las
              em texto puro, novas conexões ficam desativadas.
              {hub.value.catalog.vaultHint ? ` ${hub.value.catalog.vaultHint}` : ''}
            </InlineNotice>
          ) : (
            <p className={styles.hint}>Cofre das credenciais: {hub.value.catalog.vaultName}.</p>
          )}
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
