import { createIntegrationApi, type IntegrationApi } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, NoProjectState, Page, PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard } from '@/shared/hooks';
import type { Connector, ProjectReport } from '@/shared/schema';
import { formatEngineDate, formatRecentDate, runKindLabel, runStateLabel } from '@/shared/utils';
import { Button, SelectField } from '@mariliz/ui-kit';
import { useCallback, useMemo, useState } from 'react';

import { MilestonesBody } from './components/milestones-section';
import { ForecastSection } from './components/forecast-section';
import { FlowSection } from './components/flow-section';
import { RisksSection } from './components/risks-section';
import { reportToMarkdown } from './domain/report-markdown';
import { describeRiskChange } from './domain/risk-history-text';
import styles from './management-page.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

const PERIODS = [2, 4, 8, 12] as const;

/** O período conta semanas: a hora do fim não diz nada e só polui. */
const reportDay = (iso: string) => formatRecentDate(Date.parse(iso)).toLowerCase();

/** Tudo vem do motor; nenhuma frase é escrita por modelo. */
export function ReportPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  const integrations = useMemo(() => createIntegrationApi(client), [client]);
  const management = useMemo(() => createManagementApi(client), [client]);

  if (!project) {
    return (
      <Page title="Relatório">
        <NoProjectState />
      </Page>
    );
  }
  return (
    <ProjectReportView key={project.projectId} integrations={integrations} management={management} project={project} />
  );
}

function ProjectReportView({
  integrations,
  management,
  project
}: {
  integrations: IntegrationApi;
  management: ManagementApi;
  project: SelectedProject;
}) {
  const [weeks, setWeeks] = useState<number>(4);
  const load = useCallback(async () => {
    const [catalog, report] = await Promise.all([integrations.catalog(), management.report(project.projectId, weeks)]);
    return { connectors: catalog.connectors, report };
  }, [integrations, management, project.projectId, weeks]);
  const content = useAsyncResource(load);

  return (
    <Page
      title="Relatório"
      description={`${project.displayName} · o que aconteceu no período, a partir do que o motor leu e registrou.`}
    >
      <PageSection title="Período">
        <SelectField
          label="Largura do período"
          size="small"
          fullWidth={false}
          sx={{ minWidth: '14rem' }}
          value={weeks}
          onChange={(event) => setWeeks(Number(event.target.value))}
          options={PERIODS.map((value) => ({ label: `Últimas ${value} semanas`, value }))}
        />
      </PageSection>
      <ResourceView
        resource={content}
        loading="Montando o relatório…"
        fallbackError="Não foi possível montar o relatório."
      >
        {({ connectors, report }) => <ReportBody connectors={connectors} report={report} />}
      </ResourceView>
    </Page>
  );
}

function ReportBody({ connectors, report }: { connectors: Connector[]; report: ProjectReport }) {
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 4000 });
  const kindLabel = useCallback(
    (kind: string) =>
      connectors.flatMap((connector) => connector.dataKinds).find((entry) => entry.id === kind)?.label ?? kind,
    [connectors]
  );

  return (
    <>
      <PageSection
        title="Para levar"
        action={
          <Button variant="outlined" onClick={() => void copy(reportToMarkdown(report, kindLabel))}>
            Copiar como texto
          </Button>
        }
      >
        <p className={styles.muted}>
          Período: de {reportDay(report.periodFrom)} a {reportDay(report.periodTo)}. O texto copiado traz o mesmo
          conteúdo desta tela, em Markdown.
        </p>
        <p className={styles.muted} role="status">
          {state === 'copied'
            ? 'Relatório copiado.'
            : state === 'failed'
              ? 'Não foi possível copiar. Selecione o texto da tela e copie à mão.'
              : ''}
        </p>
      </PageSection>
      <FlowSection metrics={report.metrics} connectors={connectors} />
      <ForecastSection forecast={report.forecast} connectors={connectors} />
      <PageSection title="Marcos">
        <MilestonesBody value={report.milestones} />
      </PageSection>
      <RisksSection risks={report.risks} />
      <RiskChangesSection report={report} />
      <DecisionsInPeriod report={report} />
      <RunsInPeriod report={report} />
      <p className={styles.muted}>{report.basis}</p>
    </>
  );
}

function RiskChangesSection({ report }: { report: ProjectReport }) {
  return (
    <PageSection title="Mudanças de risco no período">
      {report.riskChanges.length === 0 ? (
        <EmptyState>Nenhuma mudança de risco registrada no período.</EmptyState>
      ) : (
        <ul className={styles.plainList} aria-label="Mudanças de risco no período">
          {report.riskChanges.map((item) => (
            <li key={`${item.riskId}:${item.change.at}:${item.change.kind}`}>
              <span className={styles.muted}>{formatEngineDate(item.change.at)}: </span>
              <span className={styles.rowTitle}>{item.title}.</span> {describeRiskChange(item.change)}
            </li>
          ))}
        </ul>
      )}
    </PageSection>
  );
}

function DecisionsInPeriod({ report }: { report: ProjectReport }) {
  return (
    <PageSection title="Decisões do período">
      {report.decisions.length === 0 ? (
        <EmptyState>Nenhuma decisão registrada no período.</EmptyState>
      ) : (
        <ul className={styles.plainList} aria-label="Decisões do período">
          {report.decisions.map((decision) => (
            <li key={decision.id}>
              <span className={styles.muted}>{formatEngineDate(decision.recordedAt)}: </span>
              <span className={styles.rowTitle}>{decision.title}.</span> {decision.decision}
            </li>
          ))}
        </ul>
      )}
    </PageSection>
  );
}

function RunsInPeriod({ report }: { report: ProjectReport }) {
  return (
    <PageSection title="Execuções do período">
      {report.runs.length === 0 ? (
        <EmptyState>Nenhuma execução começou no período.</EmptyState>
      ) : (
        <ul className={styles.plainList} aria-label="Execuções do período">
          {report.runs.map((run) => (
            <li key={`${run.kind}:${run.state}`}>
              {runKindLabel(run.kind)}, {runStateLabel(run.state).toLowerCase()}:{' '}
              <span className={styles.rowTitle}>{run.count}</span>
            </li>
          ))}
        </ul>
      )}
    </PageSection>
  );
}
