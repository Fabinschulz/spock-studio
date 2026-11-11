import {
  SYNC_STATUS,
  createIntegrationApi,
  formatEngineDate,
  kindCounts,
  type IntegrationApi
} from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import { errorMessage, type EngineClient } from '@/shared/api';
import { StatusChip } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { Connector, ProjectIntegration, ProjectMetrics } from '@/shared/schema';
import { useCallback, useMemo } from 'react';
import { Link } from 'react-router';

import { FlowSection } from './components/flow-section';
import { summarizeActivity } from './domain/activity-summary';
import styles from './management-page.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

/**
 * Visão gerencial do projeto aberto. Mostra só o que foi observado nas fontes vinculadas; o que ainda não é calculado
 * aparece dito como ausente, nunca como zero.
 */
export function ManagementPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const api = useMemo(() => createIntegrationApi(client), [client]);
  const management = useMemo(() => createManagementApi(client), [client]);

  if (!project) {
    return (
      <div className={styles.page}>
        <h1 className={styles.title}>Acompanhamento</h1>
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link>.
        </p>
      </div>
    );
  }
  return <ProjectManagement key={project.projectId} api={api} management={management} project={project} />;
}

function ProjectManagement({
  api,
  management,
  project
}: {
  api: IntegrationApi;
  management: ManagementApi;
  project: SelectedProject;
}) {
  const load = useCallback(async () => {
    const [catalog, integrations, metrics] = await Promise.all([
      api.catalog(),
      api.integrations(project.projectId),
      management.metrics(project.projectId)
    ]);
    return { connectors: catalog.connectors, integrations, metrics };
  }, [api, management, project.projectId]);
  const sources = useAsyncResource(load);

  return (
    <div className={styles.page}>
      <header>
        <h1 className={styles.title}>Acompanhamento</h1>
        <p className={styles.lead}>
          {project.displayName} · o andamento do projeto, a partir do que foi lido das ferramentas vinculadas a ele.
        </p>
      </header>

      {sources.status === 'loading' ? <p role="status">Lendo as fontes do projeto…</p> : null}
      {sources.status === 'error' ? (
        <p role="alert">{errorMessage(sources.error, 'Não foi possível ler as fontes do projeto.')}</p>
      ) : null}
      {sources.status === 'ready' ? (
        <>
          <ActivitySection integrations={sources.value.integrations} />
          <FlowSection metrics={sources.value.metrics} connectors={sources.value.connectors} />
          <SourcesSection integrations={sources.value.integrations} connectors={sources.value.connectors} />
          <NotCalculatedSection metrics={sources.value.metrics} />
        </>
      ) : null}
    </div>
  );
}

function ActivitySection({ integrations }: { integrations: ProjectIntegration[] }) {
  const summary = summarizeActivity(integrations);

  return (
    <section aria-labelledby="atividades">
      <h2 id="atividades" className={styles.section}>
        Atividades
      </h2>
      {integrations.length === 0 ? (
        <p>
          Não há dados de atividades: nenhuma ferramenta foi vinculada a este projeto. Vincule uma em{' '}
          <Link to={pathTo('integrations')}>Integrações</Link>.
        </p>
      ) : summary.total === 0 ? (
        <p>
          Não há dados de atividades: as fontes vinculadas ainda não trouxeram nenhum item. Sincronize em{' '}
          <Link to={pathTo('integrations')}>Integrações</Link>.
        </p>
      ) : (
        <>
          <dl className={styles.figures}>
            {summary.byCategory.map((entry) => (
              <div key={entry.category} className={styles.figure}>
                <dt>{entry.label}</dt>
                <dd>{entry.count}</dd>
              </div>
            ))}
          </dl>
          <p className={styles.muted}>
            Contagem do que foi sincronizado
            {summary.oldestReadAt ? `; a leitura mais antiga é de ${formatEngineDate(summary.oldestReadAt)}` : ''}. No
            GitHub, issue aberta conta como a fazer e pull request aberto como em andamento; no Jira vale a categoria do
            status.
          </p>
        </>
      )}
    </section>
  );
}

function SourcesSection({ integrations, connectors }: { integrations: ProjectIntegration[]; connectors: Connector[] }) {
  if (integrations.length === 0) return null;

  return (
    <section aria-labelledby="fontes">
      <h2 id="fontes" className={styles.section}>
        Fontes de dados
      </h2>
      <ul className={styles.rows}>
        {integrations.map((integration) => {
          const connector = connectors.find((entry) => entry.provider === integration.provider);
          const kinds = kindCounts(integration, connector);
          return (
            <li key={integration.id} className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{integration.scope.resourceName}</span>
                <span className={styles.muted}>
                  {' '}
                  · {connector?.name ?? integration.provider}
                  {kinds.length > 0 ? ` · ${kinds.join(', ')}` : ' · sem itens sincronizados'}
                  {integration.items.lastSyncedAt ? ` · lido ${formatEngineDate(integration.items.lastSyncedAt)}` : ''}
                </span>
              </span>
              {integration.lastSync ? (
                <StatusChip tone={SYNC_STATUS[integration.lastSync.status].tone}>
                  {SYNC_STATUS[integration.lastSync.status].label}
                </StatusChip>
              ) : null}
            </li>
          );
        })}
      </ul>
      <p className={styles.muted}>
        Conexões, recortes e sincronização ficam em <Link to={pathTo('integrations')}>Integrações</Link>.
      </p>
    </section>
  );
}

/** O que foi pedido e ainda não é calculado, com o motivo dado pelo motor. Nenhum desses aparece com valor. */
function NotCalculatedSection({ metrics }: { metrics: ProjectMetrics }) {
  return (
    <section aria-labelledby="ainda-nao-calculado">
      <h2 id="ainda-nao-calculado" className={styles.section}>
        Ainda não calculado
      </h2>
      <ul className={styles.plain}>
        {metrics.notCalculated.map((entry) => (
          <li key={entry.id}>
            <span className={styles.rowTitle}>{entry.label}.</span> <span className={styles.muted}>{entry.reason}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
