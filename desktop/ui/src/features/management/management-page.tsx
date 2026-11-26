import { createIntegrationApi, type IntegrationApi } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { NoProjectState, Page, ResourceView } from '@/shared/components';
import { useAsyncResource } from '@/shared/hooks';
import type { Connector, ProjectForecast, ProjectIntegration, ProjectMetrics, ProjectRisks } from '@/shared/schema';
import { TabBar } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';

import { ActivitySection } from './components/activity-section';
import { AttentionStrip } from './components/attention-strip';
import { DecisionsSection } from './components/decisions-section';
import { FlowSection } from './components/flow-section';
import { ForecastSection } from './components/forecast-section';
import { LinkedRunsSection } from './components/linked-runs-section';
import { MilestonesSection } from './components/milestones-section';
import { NoSourcesState } from './components/no-sources-state';
import { NotCalculated } from './components/not-calculated';
import { PeriodField } from './components/period-field';
import { RiskHistorySection } from './components/risk-history-section';
import { RisksSection } from './components/risks-section';
import { SourcesSection } from './components/sources-section';
import { summarizeActivity } from './domain/activity-summary';
import { summarizeAttention } from './domain/attention';
import { usePeriod } from './hooks/use-period';
import styles from './management.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

/** O que ainda não é calculado aparece dito como ausente, nunca como zero. */
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

interface Loaded {
  connectors: Connector[];
  integrations: ProjectIntegration[];
  metrics: ProjectMetrics;
  forecast: ProjectForecast;
  risks: ProjectRisks;
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
  const [weeks, setWeeks] = usePeriod();
  const load = useCallback(async (): Promise<Loaded> => {
    const [catalog, integrations, metrics, forecast, risks] = await Promise.all([
      api.catalog(),
      api.integrations(project.projectId),
      management.metrics(project.projectId, weeks),
      management.forecast(project.projectId),
      management.risks(project.projectId)
    ]);
    return { connectors: catalog.connectors, integrations, metrics, forecast, risks };
  }, [api, management, project.projectId, weeks]);
  const sources = useAsyncResource(load);

  return (
    <Page
      title="Acompanhamento"
      description={`${project.displayName} · o andamento do projeto, a partir do que foi lido das ferramentas vinculadas a ele.`}
      actions={<PeriodField weeks={weeks} onChange={setWeeks} />}
    >
      <ResourceView
        resource={sources}
        loading="Lendo as fontes do projeto…"
        fallbackError="Não foi possível ler as fontes do projeto."
      >
        {(value) =>
          value.integrations.length === 0 ? (
            <>
              <NoSourcesState />
              <DecisionsSection management={management} projectId={project.projectId} />
              <NotCalculated metrics={value.metrics} />
            </>
          ) : (
            <WithSources value={value} management={management} projectId={project.projectId} />
          )
        }
      </ResourceView>
    </Page>
  );
}

interface WithSourcesProps {
  value: Loaded;
  management: ManagementApi;
  projectId: string;
}

/** A faixa vem antes das abas e responde sozinha; cada aba só carrega o que as seções dela pedem. */
function WithSources({ value, management, projectId }: WithSourcesProps) {
  const { connectors, integrations, metrics, forecast, risks } = value;
  const activity = summarizeActivity(integrations);
  const attention = summarizeAttention({ risks, metrics, oldestReadAt: activity.oldestReadAt });
  const activeRisks = risks.hasData ? risks.risks.length : 0;

  return (
    <>
      <AttentionStrip attention={attention} oldestReadAt={activity.oldestReadAt} />
      <TabBar
        aria-label="Acompanhamento do projeto"
        tabs={[
          { label: 'Visão geral' },
          { label: 'Entregas' },
          { label: activeRisks > 0 ? `Riscos (${activeRisks})` : 'Riscos' },
          { label: 'Decisões' },
          { label: 'Fontes' }
        ]}
      >
        <div className={styles.tab}>
          <ActivitySection integrations={integrations} />
          <ForecastSection forecast={forecast} connectors={connectors} />
        </div>
        <div className={styles.tab}>
          <FlowSection metrics={metrics} connectors={connectors} />
          <MilestonesSection management={management} projectId={projectId} />
        </div>
        <div className={styles.tab}>
          <RisksSection risks={risks} />
          <RiskHistorySection management={management} projectId={projectId} />
        </div>
        <div className={styles.tab}>
          <DecisionsSection management={management} projectId={projectId} />
        </div>
        <div className={styles.tab}>
          <SourcesSection integrations={integrations} connectors={connectors} />
          <LinkedRunsSection management={management} projectId={projectId} />
          <NotCalculated metrics={metrics} />
        </div>
      </TabBar>
    </>
  );
}
