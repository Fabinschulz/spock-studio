import { createIntegrationApi, type IntegrationApi } from '@/features/integrations';
import type { SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, NoProjectState, Page, PageSection, ResourceView } from '@/shared/components';
import { useAsyncResource, useCopyToClipboard, type CopyState } from '@/shared/hooks';
import type { Connector, ProjectReport } from '@/shared/schema';
import { formatEngineDate, formatRecentDate } from '@/shared/utils';
import { Button } from '@mariliz/ui-kit';
import { useCallback, useMemo } from 'react';

import { MilestonesBody } from './components/milestones-section';
import { ForecastSection } from './components/forecast-section';
import { PeriodField } from './components/period-field';
import { FlowSection } from './components/flow-section';
import { RisksSection } from './components/risks-section';
import { reportToMarkdown } from './domain/report-markdown';
import { describeRiskChange } from './domain/risk-history-text';
import { describeRunCount } from './domain/section-text';
import { usePeriod } from './hooks/use-period';
import styles from './management.module.scss';
import { createManagementApi, type ManagementApi } from './service/management-api';

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
  const [weeks, setWeeks] = usePeriod();
  const load = useCallback(async () => {
    const [catalog, report] = await Promise.all([integrations.catalog(), management.report(project.projectId, weeks)]);
    return { connectors: catalog.connectors, report };
  }, [integrations, management, project.projectId, weeks]);
  const content = useAsyncResource(load);
  const { state, copy } = useCopyToClipboard({ resetAfterMs: 4000 });
  const ready = content.status === 'ready' ? content.value : null;

  return (
    <Page
      title="Relatório"
      description={`${project.displayName} · o que aconteceu no período, a partir do que o motor leu e registrou.`}
      actions={
        <>
          <PeriodField weeks={weeks} onChange={setWeeks} />
          {ready ? (
            <Button
              variant="contained"
              onClick={() => void copy(reportToMarkdown(ready.report, kindLabeler(ready.connectors)))}
            >
              Copiar como Markdown
            </Button>
          ) : null}
        </>
      }
    >
      <ResourceView
        resource={content}
        loading="Montando o relatório…"
        fallbackError="Não foi possível montar o relatório."
      >
        {({ connectors, report }) => <ReportBody connectors={connectors} report={report} copyState={state} />}
      </ResourceView>
    </Page>
  );
}

const kindLabeler = (connectors: Connector[]) => (kind: string) =>
  connectors.flatMap((connector) => connector.dataKinds).find((entry) => entry.id === kind)?.label ?? kind;

/** As seções seguem a ordem das abas do Acompanhamento; uma sem dado diz o que falta, e não some. */
function ReportBody({
  connectors,
  report,
  copyState
}: {
  connectors: Connector[];
  report: ProjectReport;
  copyState: CopyState;
}) {
  return (
    <>
      <div>
        <p className={styles.muted}>
          Período: de {reportDay(report.periodFrom)} a {reportDay(report.periodTo)}. O texto copiado traz o mesmo
          conteúdo desta tela, em Markdown.
        </p>
        <p className={styles.muted} role="status">
          {copyState === 'copied'
            ? 'Relatório copiado.'
            : copyState === 'failed'
              ? 'Não foi possível copiar. Selecione o texto da tela e copie à mão.'
              : ''}
        </p>
      </div>
      <ForecastSection forecast={report.forecast} connectors={connectors} />
      <FlowSection metrics={report.metrics} connectors={connectors} />
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
            <li key={`${run.kind}:${run.state}`}>{describeRunCount(run)}</li>
          ))}
        </ul>
      )}
    </PageSection>
  );
}
