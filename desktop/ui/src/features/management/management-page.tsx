import {
  SYNC_STATUS,
  createIntegrationApi,
  formatEngineDate,
  kindCounts,
  type IntegrationApi
} from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { StatusChip, EmptyState, NoProjectState, Page, PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import { pathTo } from '@/shared/routing';
import type { Connector, ProjectIntegration, ProjectMetrics } from '@/shared/schema';
import { useCallback, useMemo } from 'react';
import { Link } from 'react-router';

import { FlowSection } from './components/flow-section';
import { LinkedRunsSection } from './components/linked-runs-section';
import { MilestonesSection } from './components/milestones-section';
import { ForecastSection } from './components/forecast-section';
import { RiskHistorySection } from './components/risk-history-section';
import { RisksSection } from './components/risks-section';
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
      <Page title="Acompanhamento">
        <NoProjectState />
      </Page>
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
    const [catalog, integrations, metrics, forecast, risks] = await Promise.all([
      api.catalog(),
      api.integrations(project.projectId),
      management.metrics(project.projectId),
      management.forecast(project.projectId),
      management.risks(project.projectId)
    ]);
    return { connectors: catalog.connectors, integrations, metrics, forecast, risks };
  }, [api, management, project.projectId]);
  const sources = useAsyncResource(load);

  return (
    <Page
      title="Acompanhamento"
      description={`${project.displayName} · o andamento do projeto, a partir do que foi lido das ferramentas vinculadas a ele.`}
    >
      <ResourceView
        resource={sources}
        loading="Lendo as fontes do projeto…"
        fallbackError="Não foi possível ler as fontes do projeto."
      >
        {(value) => (
          <>
            <ActivitySection integrations={value.integrations} />
            <RisksSection risks={value.risks} />
            {value.integrations.length > 0 ? (
              <RiskHistorySection management={management} projectId={project.projectId} />
            ) : null}
            {value.integrations.length > 0 ? (
              <LinkedRunsSection management={management} projectId={project.projectId} />
            ) : null}
            <ForecastSection forecast={value.forecast} connectors={value.connectors} />
            {value.integrations.length > 0 ? (
              <MilestonesSection management={management} projectId={project.projectId} />
            ) : null}
            <FlowSection metrics={value.metrics} connectors={value.connectors} />
            <SourcesSection integrations={value.integrations} connectors={value.connectors} />
            <NotCalculatedSection metrics={value.metrics} />
          </>
        )}
      </ResourceView>
    </Page>
  );
}

function ActivitySection({ integrations }: { integrations: ProjectIntegration[] }) {
  const summary = summarizeActivity(integrations);

  return (
    <PageSection title="Atividades">
      {integrations.length === 0 ? (
        <EmptyState>
          Não há dados de atividades: nenhuma ferramenta foi vinculada a este projeto. Vincule uma em{' '}
          <Link to={pathTo('integrations')}>Integrações</Link>.
        </EmptyState>
      ) : summary.total === 0 ? (
        <EmptyState>
          Não há dados de atividades: as fontes vinculadas ainda não trouxeram nenhum item. Sincronize em{' '}
          <Link to={pathTo('integrations')}>Integrações</Link>.
        </EmptyState>
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
    </PageSection>
  );
}

function SourcesSection({ integrations, connectors }: { integrations: ProjectIntegration[]; connectors: Connector[] }) {
  if (integrations.length === 0) return null;

  return (
    <PageSection title="Fontes de dados">
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
    </PageSection>
  );
}

/** O que foi pedido e ainda não é calculado, com o motivo dado pelo motor. Nenhum desses aparece com valor. */
function NotCalculatedSection({ metrics }: { metrics: ProjectMetrics }) {
  return (
    <PageSection title="Ainda não calculado">
      <ul className={styles.plain}>
        {metrics.notCalculated.map((entry) => (
          <li key={entry.id}>
            <span className={styles.rowTitle}>{entry.label}.</span> <span className={styles.muted}>{entry.reason}</span>
          </li>
        ))}
      </ul>
    </PageSection>
  );
}
