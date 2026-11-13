import { formatRecentDateTime, type SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { EmptyState, ErrorState, LoadingState, Page, PageSection } from '@/shared/components';
import { pathTo } from '@/shared/routing';
import type { Discovery, RunSummary } from '@/shared/schema';
import { runKindLabel } from '@/shared/utils';
import { Link } from 'react-router';

import { ProfileSection } from './components/profile-section';
import { runRowText } from './domain/run-labels';
import { useProjectOverview } from './hooks/use-project-overview';
import { ProjectPageFrame } from './project-page-frame';
import styles from './project-page.module.scss';

const INVENTORY_ROWS: readonly [string, keyof Discovery['inventory']][] = [
  ['Agentes', 'agents'],
  ['Skills', 'skills'],
  ['Regras', 'rules'],
  ['Hooks', 'hooks'],
  ['Servidores MCP', 'mcpServers'],
  ['Evals', 'evals']
];

/**
 * A visão geral do projeto na Engenharia: o que ele usa, o que já existe de configuração de IA e o que foi executado.
 * O Harness aplicado e o conhecimento têm tela própria, para esta não virar um relatório sem fim.
 */
export function ProjectPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  return (
    <ProjectPageFrame project={project} title="Visão geral">
      {(selected) => <ProjectOverview client={client} project={selected} />}
    </ProjectPageFrame>
  );
}

function ProjectOverview({ client, project }: { client: EngineClient; project: SelectedProject }) {
  const overview = useProjectOverview(client, project.projectId);

  return (
    <Page title={project.displayName} description={<span className={styles.path}>{project.displayPath}</span>}>
      {overview.status === 'loading' ? <LoadingState>Lendo o projeto…</LoadingState> : null}
      {overview.status === 'error' ? <ErrorState>{overview.message}</ErrorState> : null}
      {overview.status === 'ready' ? (
        <>
          <ProfileSection client={client} projectId={project.projectId} />
          <InventorySection discovery={overview.discovery} />
          <RunsSection runs={overview.runs} />
        </>
      ) : null}
      <MoreSection />
    </Page>
  );
}

/** Para onde ir a partir da visão geral. O menu tem os mesmos destinos; aqui eles vêm com uma frase do que há lá. */
function MoreSection() {
  return (
    <PageSection title="Mais sobre o projeto">
      <ul className={styles.rows}>
        <li className={styles.row}>
          <span>
            <Link to={pathTo('projectHarness')}>Harness</Link>
            <span className={styles.meta}>
              {' '}
              · o que foi aplicado no projeto, conferido contra o disco, e as revisões
            </span>
          </span>
        </li>
        <li className={styles.row}>
          <span>
            <Link to={pathTo('projectKnowledge')}>Conhecimento</Link>
            <span className={styles.meta}> · o que os agentes podem ler do projeto e o que fica de fora</span>
          </span>
        </li>
      </ul>
    </PageSection>
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
