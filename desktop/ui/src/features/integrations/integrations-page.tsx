import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { InlineNotice } from '@/shared/components';
import { useCallback, useMemo } from 'react';

import { ConnectionsSection } from './components/connections-section';
import { ProjectIntegrationsSection } from './components/project-integrations-section';
import { useReloadable } from './hooks/use-reloadable';
import styles from './integrations-page.module.scss';
import { createIntegrationApi } from './service/integration-api';

/** Central de integrações: as conexões do computador e, para o projeto aberto, o que ele pode ler de cada uma. */
export function IntegrationsPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIntegrationApi(client), [client]);
  const load = useCallback(async () => {
    const [catalog, connections] = await Promise.all([api.catalog(), api.connections()]);
    return { catalog, connections };
  }, [api]);
  const hub = useReloadable(load);

  return (
    <div className={styles.page}>
      <header>
        <h1 className={styles.title}>Integrações</h1>
        <p className={styles.lead}>
          Conecte as ferramentas da equipe e decida, projeto a projeto, o que o Mariliz pode ler. As credenciais ficam
          protegidas neste computador e não aparecem em nenhuma tela.
        </p>
      </header>

      {!hub.settled ? <p role="status">Lendo as integrações…</p> : null}
      {hub.settled && hub.value === null ? (
        <p role="alert">{errorMessage(hub.error, 'Não foi possível ler as integrações.')}</p>
      ) : null}

      {hub.value ? (
        <>
          {!hub.value.catalog.vaultAvailable ? (
            <InlineNotice tone="warning" title="Armazenamento protegido indisponível">
              Neste sistema o Mariliz ainda não tem onde guardar credenciais com proteção do sistema operacional. Para
              não gravá-las em texto puro, novas conexões ficam desativadas.
            </InlineNotice>
          ) : null}
          <ConnectionsSection
            api={api}
            catalog={hub.value.catalog}
            connections={hub.value.connections}
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
    </div>
  );
}
