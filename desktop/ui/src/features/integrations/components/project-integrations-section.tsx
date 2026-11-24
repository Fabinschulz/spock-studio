import { useAction, useReloadable } from '@/shared/hooks';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { Connection, IntegrationCatalog } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';

import { ErrorState, LoadingState, PageSection, SuccessState } from '@/shared/components';
import styles from '../integrations-page.module.scss';
import type { IntegrationApi } from '../service/integration-api';
import { IntegrationRow } from './integration-row';
import { LinkForm } from './link-form';

/**
 * Enquanto uma sincronização anda, o motor avisa por evento e a lista se atualiza na hora. Esta leitura periódica é só a
 * rede de segurança, para um aviso perdido (a janela foi suspensa, por exemplo) não deixar a tela presa em "Sincronizando".
 */
const SYNC_FALLBACK_POLL_MS = 15_000;

interface ProjectIntegrationsSectionProps {
  api: IntegrationApi;
  project: SelectedProject | null;
  catalog: IntegrationCatalog;
  connections: Connection[];
  onChanged: () => void;
}

export function ProjectIntegrationsSection({ project, ...rest }: ProjectIntegrationsSectionProps) {
  return (
    <PageSection title={project ? `Neste projeto: ${project.displayName}` : 'Neste projeto'}>
      {project ? (
        // A chave remonta a lista ao trocar de projeto: nenhum vínculo de um projeto aparece, nem por um instante, em outro.
        <ProjectIntegrations key={project.projectId} project={project} {...rest} />
      ) : (
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link> para vincular
          conexões a ela.
        </p>
      )}
    </PageSection>
  );
}

function ProjectIntegrations({
  api,
  project,
  catalog,
  connections,
  onChanged
}: ProjectIntegrationsSectionProps & { project: SelectedProject }) {
  const { projectId } = project;
  const load = useCallback(() => api.integrations(projectId), [api, projectId]);
  const list = useReloadable(load);
  const reloadList = list.reload;
  const reload = useCallback(() => {
    reloadList();
    onChanged();
  }, [reloadList, onChanged]);
  const { busy, error, notice, run } = useAction(reload);
  const [linking, setLinking] = useState<string | null>(null);

  useEffect(() => {
    let stop: (() => void) | undefined;
    let active = true;
    api
      .onSyncEvent((event) => {
        if (event.data.projectId === projectId) reload();
      })
      .then(
        (unlisten) => {
          if (active) stop = unlisten;
          else unlisten();
        },
        () => undefined
      );
    return () => {
      active = false;
      stop?.();
    };
  }, [api, projectId, reload]);

  const integrations = list.value;
  const syncing = integrations?.some((integration) => integration.syncing) ?? false;
  useEffect(() => {
    if (!syncing) return;
    const timer = setTimeout(reload, SYNC_FALLBACK_POLL_MS);
    return () => clearTimeout(timer);
  }, [syncing, integrations, reload]);

  if (!list.settled) return <LoadingState>Lendo os vínculos do projeto…</LoadingState>;
  if (integrations === null)
    return (
      <ErrorState onRetry={reloadList}>
        {errorMessage(list.error, 'Não foi possível ler os vínculos do projeto.')}
      </ErrorState>
    );

  const linkable = connections.filter((connection) => connection.status === 'connected');
  const linkingConnection = linkable.find((connection) => connection.id === linking);
  const linkingConnector = catalog.connectors.find((connector) => connector.provider === linkingConnection?.provider);

  return (
    <>
      <p className={styles.sectionLead}>
        O que este projeto pode ler de cada conexão. Fora do recurso e dos tipos marcados aqui, nada é consultado.
      </p>

      {integrations.length === 0 ? <p>Nenhuma conexão vinculada a este projeto.</p> : null}
      <ul className={styles.rows}>
        {integrations.map((integration) => (
          <IntegrationRow
            key={integration.id}
            integration={integration}
            connector={catalog.connectors.find((connector) => connector.provider === integration.provider)}
            busy={busy}
            onSync={() => void run(() => api.sync(projectId, integration.id), 'Não foi possível sincronizar.')}
            onConfigure={(change) =>
              void run(
                () => api.configure({ projectId, integrationId: integration.id, ...change }),
                'Não foi possível mudar o recorte.'
              )
            }
            onUnlink={() =>
              void run(() => api.unlink(projectId, integration.id), 'Não foi possível desvincular.', {
                success: () => `Vínculo com ${integration.scope.resourceName} removido.`
              })
            }
            loadHistory={() => api.history(projectId, integration.id)}
          />
        ))}
      </ul>

      {notice ? <SuccessState>{notice.text}</SuccessState> : null}
      {error ? <ErrorState>{error}</ErrorState> : null}

      {linkingConnection && linkingConnector ? (
        <LinkForm
          key={linkingConnection.id}
          connection={linkingConnection}
          connector={linkingConnector}
          busy={busy}
          onCancel={() => setLinking(null)}
          onSubmit={(resource, dataKinds) =>
            void run(
              () => api.link({ projectId, connectionId: linkingConnection.id, resource, dataKinds }),
              'Não foi possível vincular.',
              {
                success: (linked) =>
                  `${linked.scope.resourceName} vinculado a este projeto. Sincronize para trazer os dados.`
              }
            ).then((ok) => {
              if (ok) setLinking(null);
            })
          }
        />
      ) : linkable.length > 0 ? (
        <div className={styles.actions}>
          {linkable.map((connection) => (
            <Button
              key={connection.id}
              type="button"
              variant="outlined"
              disabled={busy}
              onClick={() => setLinking(connection.id)}
            >
              Vincular {connection.name}
            </Button>
          ))}
        </div>
      ) : (
        <p className={styles.meta}>Para vincular, é preciso ter uma conexão ativa acima.</p>
      )}
    </>
  );
}
