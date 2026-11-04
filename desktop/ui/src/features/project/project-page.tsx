import { formatRecentDateTime, type SelectedProject } from '@/features/project-selection';
import type { EngineClient } from '@/shared/api';
import { pathTo } from '@/shared/routing';
import type { Discovery, RunSummary } from '@/shared/schema';
import { Link } from 'react-router';

import { runKindLabel, runRowText } from './domain/run-labels';
import { useProjectOverview } from './hooks/use-project-overview';
import styles from './project-page.module.scss';

const INVENTORY_ROWS: readonly [string, keyof Discovery['inventory']][] = [
  ['Agentes', 'agents'],
  ['Skills', 'skills'],
  ['Regras', 'rules'],
  ['Hooks', 'hooks'],
  ['Servidores MCP', 'mcpServers'],
  ['Evals', 'evals']
];

const TOOL_KIND_LABEL: Record<string, string> = { build: 'Build', test: 'Teste' };

export function ProjectPage({ client, project }: { client: EngineClient; project: SelectedProject | null }) {
  if (!project) {
    return (
      <div className={styles.page}>
        <h1 className={styles.title}>Projeto</h1>
        <p>
          Nenhum projeto selecionado. Escolha uma pasta na <Link to={pathTo('home')}>tela inicial</Link>.
        </p>
      </div>
    );
  }
  return <ProjectContent key={project.projectId} client={client} project={project} />;
}

function ProjectContent({ client, project }: { client: EngineClient; project: SelectedProject }) {
  const overview = useProjectOverview(client, project.projectId);

  return (
    <div className={styles.page}>
      <header>
        <h1 className={styles.title}>{project.displayName}</h1>
        <p className={styles.path}>{project.displayPath}</p>
      </header>

      {overview.status === 'loading' ? <p role="status">Lendo o projeto…</p> : null}
      {overview.status === 'error' ? <p role="alert">{overview.message}</p> : null}
      {overview.status === 'ready' ? (
        <>
          <InventorySection discovery={overview.discovery} />
          <StackSection discovery={overview.discovery} />
          <RunsSection runs={overview.runs} />
        </>
      ) : null}
    </div>
  );
}

function InventorySection({ discovery }: { discovery: Discovery }) {
  return (
    <section aria-labelledby="inventario">
      <h2 id="inventario" className={styles.section}>
        O que já existe
      </h2>
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
    </section>
  );
}

function StackSection({ discovery }: { discovery: Discovery }) {
  const empty = discovery.stacks.length === 0 && discovery.tooling.length === 0;
  return (
    <section aria-labelledby="stack">
      <h2 id="stack" className={styles.section}>
        Stack e comandos
      </h2>
      {empty ? <p>A análise não identificou stack nem comandos de build e teste.</p> : null}
      <ul className={styles.rows}>
        {discovery.stacks.map((stack) => (
          <li key={`${stack.id}:${stack.root}`} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>{stack.name}</span>
              <span className={styles.meta}>
                {stack.version ? ` ${stack.version}` : ''} · em {stack.root}
              </span>
            </span>
            <span className={styles.end}>{CONFIDENCE[stack.confidence]}</span>
          </li>
        ))}
        {discovery.tooling.map((tool) => (
          <li key={`${tool.kind}:${tool.name}`} className={styles.row}>
            <span>
              <span className={styles.rowTitle}>{TOOL_KIND_LABEL[tool.kind] ?? tool.kind}</span>{' '}
              {tool.command ? (
                <span className={styles.mono}>{tool.command}</span>
              ) : (
                <span className={styles.meta}>{tool.name}</span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

const CONFIDENCE = { high: 'confiança alta', medium: 'confiança média', low: 'confiança baixa' } as const;

function RunsSection({ runs }: { runs: RunSummary[] }) {
  return (
    <section aria-labelledby="execucoes">
      <h2 id="execucoes" className={styles.section}>
        Execuções
      </h2>
      {runs.length === 0 ? <p>Nenhuma execução neste projeto ainda.</p> : null}
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
    </section>
  );
}

function formatRunDate(startedAt: string): string {
  const ms = Date.parse(startedAt);
  return Number.isNaN(ms) ? startedAt : formatRecentDateTime(ms);
}
