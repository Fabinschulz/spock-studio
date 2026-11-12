import { formatRecentDateTime, type SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import type { Discovery, RunSummary } from '@/shared/schema';

import { HarnessSection } from './components/harness-section';
import { KnowledgeSection } from './components/knowledge-section';
import { ProfileSection } from './components/profile-section';
import { RevisionsSection } from './components/revisions-section';
import { runKindLabel, runRowText } from './domain/run-labels';
import { useProjectOverview } from './hooks/use-project-overview';
import { EmptyState, ErrorState, LoadingState, NoProjectState, Page, PageSection } from '@/shared/components';
import styles from './project-page.module.scss';

const INVENTORY_ROWS: readonly [string, keyof Discovery['inventory']][] = [
  ['Agentes', 'agents'],
  ['Skills', 'skills'],
  ['Regras', 'rules'],
  ['Hooks', 'hooks'],
  ['Servidores MCP', 'mcpServers'],
  ['Evals', 'evals']
];

export function ProjectPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  if (!project) {
    return (
      <Page title="Projeto">
        <NoProjectState />
      </Page>
    );
  }
  return <ProjectContent key={project.projectId} client={client} project={project} />;
}

function ProjectContent({ client, project }: { client: EngineClient; project: SelectedProject }) {
  const overview = useProjectOverview(client, project.projectId);

  return (
    <Page title={project.displayName} description={<span className={styles.path}>{project.displayPath}</span>}>
      {overview.status === 'loading' ? <LoadingState>Lendo o projeto…</LoadingState> : null}
      {overview.status === 'error' ? <ErrorState>{overview.message}</ErrorState> : null}
      {overview.status === 'ready' ? (
        <>
          <ProfileSection client={client} projectId={project.projectId} />
          <HarnessSection client={client} projectId={project.projectId} />
          <RevisionsSection client={client} projectId={project.projectId} />
          <InventorySection discovery={overview.discovery} />
          <KnowledgeSection client={client} projectId={project.projectId} />
          <RunsSection runs={overview.runs} />
        </>
      ) : null}
    </Page>
  );
}

function InventorySection({ discovery }: { discovery: Discovery }) {
  return (
    <PageSection title="O que já existe">
      <ul className={styles.rows}>
        {INVENTORY_ROWS.map(([label, key]) => {
          const items = discovery.inventory[key];
          const unmanaged = items.some((item) => !item.managed);
          return (
            <li key={key} className={styles.row}>
              <span>
                <span className={styles.rowTitle}>{label}</span>
                {items.length > 0 ? (
                  <span className={styles.meta}> {items.map((item) => item.name).join(', ')}</span>
                ) : null}
              </span>
              <span className={styles.end}>
                {items.length > 0 ? items.length : 'nenhum'}
                {unmanaged ? ' · não gerenciado' : ''}
              </span>
            </li>
          );
        })}
      </ul>
    </PageSection>
  );
}

function RunsSection({ runs }: { runs: RunSummary[] }) {
  return (
    <PageSection title="Execuções">
      {runs.length === 0 ? <EmptyState>Nenhuma execução neste projeto ainda.</EmptyState> : null}
      <ul className={styles.rows}>
        {runs.map((run) => (
          <li key={run.runId} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>{runKindLabel(run.kind)}</span>
              <span className={styles.meta}> · {runRowText(run)}</span>
            </span>
            <span className={styles.end}>{formatRunDate(run.startedAt)}</span>
          </li>
        ))}
      </ul>
    </PageSection>
  );
}

function formatRunDate(startedAt: string): string {
  const ms = Date.parse(startedAt);
  return Number.isNaN(ms) ? startedAt : formatRecentDateTime(ms);
}
