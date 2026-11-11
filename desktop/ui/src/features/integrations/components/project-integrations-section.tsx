import type { SelectedProject } from '@/features/project-selection';
import { errorMessage } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { Connection, IntegrationCatalog } from '@/shared/schema';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router';

import { useAction } from '../hooks/use-action';
import { useReloadable } from '../hooks/use-reloadable';
import styles from '../integrations-page.module.scss';
import type { IntegrationApi } from '../service/integration-api';
import { IntegrationRow } from './integration-row';
import { LinkForm } from './link-form';

const SYNC_POLL_MS = 1500;

interface ProjectIntegrationsSectionProps {
  api: IntegrationApi;
  project: SelectedProject | null;
  catalog: IntegrationCatalog;
  connections: Connection[];
  /** Vínculos e sincronizações mudam o que a lista de conexões mostra (uso, última leitura). */
  onChanged: () => void;
}

export function ProjectIntegrationsSection({ project, ...rest }: ProjectIntegrationsSectionProps) {
  return (
    <section aria-labelledby="neste-projeto">
      <h2 id="neste-projeto" className={styles.section}>
        {project ? `Neste projeto: ${project.displayName}` : 'Neste projeto'}
      </h2>
      {project ? (
        // A chave remonta a lista ao trocar de projeto: nenhum vínculo de um projeto aparece, nem por um instante, em outro.
        <ProjectIntegrations key={project.projectId} project={project} {...rest} />
      ) : (
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link> para vincular
          conexões a ela.
        </p>
      )}
    </section>
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
  const { busy, error, run } = useAction(reload);
  const [linking, setLinking] = useState<string | null>(null);

  // O motor sincroniza em segundo plano e não avisa por evento: enquanto houver uma em andamento, a lista é relida.
  const integrations = list.value;
  const syncing = integrations?.some((integration) => integration.syncing) ?? false;
  useEffect(() => {
    if (!syncing) return;
    const timer = setTimeout(reload, SYNC_POLL_MS);
    return () => clearTimeout(timer);
  }, [syncing, integrations, reload]);

  if (!list.settled) return <p role="status">Lendo os vínculos do projeto…</p>;
  if (integrations === null)
    return <p role="alert">{errorMessage(list.error, 'Não foi possível ler os vínculos do projeto.')}</p>;

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
            onUnlink={() => void run(() => api.unlink(projectId, integration.id), 'Não foi possível desvincular.')}
            loadHistory={() => api.history(projectId, integration.id)}
          />
        ))}
      </ul>

      {error ? (
        <p className={styles.error} role="alert">
          {error}
        </p>
      ) : null}

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
              'Não foi possível vincular.'
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
