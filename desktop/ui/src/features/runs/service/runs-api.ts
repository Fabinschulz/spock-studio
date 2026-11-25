import type { EngineClient } from '@/shared/api';
import {
  runDetailSchema,
  runLinksResultSchema,
  runListSchema,
  type RunDetail,
  type RunItem,
  type RunLink,
  type RunList
} from '@/shared/schema';

export const PAGE_SIZE = 25;

export interface RunFilters {
  /** Sem projeto, valem todos os autorizados. */
  projectId: string | null;
  kind: string | null;
  state: string | null;
}

/** `cursor` é o que a página anterior devolveu; sem ele, vem a primeira. */
export function listRuns(client: EngineClient, filters: RunFilters, cursor: string | null): Promise<RunList> {
  return client.request(
    'run.list',
    {
      ...(filters.projectId ? { projectId: filters.projectId } : {}),
      ...(filters.kind ? { kinds: [filters.kind] } : {}),
      ...(filters.state ? { states: [filters.state] } : {}),
      limit: PAGE_SIZE,
      ...(cursor ? { cursor } : {})
    },
    runListSchema
  );
}

export interface RunActivity {
  running: number;
  awaitingApproval: number;
}

/** Pede uma execução só: o que interessa é o resumo, para o indicador do menu. */
export async function readRunActivity(client: EngineClient): Promise<RunActivity> {
  const { summary } = await client.request('run.list', { limit: 1 }, runListSchema);
  return { running: summary.running, awaitingApproval: summary.awaitingApproval };
}

/** Só liga itens já sincronizados do projeto. Devolve as ligações depois da mudança. */
export async function linkRun(
  client: EngineClient,
  projectId: string,
  runId: string,
  externalId: string
): Promise<RunLink[]> {
  const { links } = await client.request('run.link', { projectId, runId, externalId }, runLinksResultSchema);
  return links;
}

export async function unlinkRun(
  client: EngineClient,
  projectId: string,
  runId: string,
  link: RunLink
): Promise<RunLink[]> {
  const { links } = await client.request(
    'run.unlink',
    { projectId, runId, integrationId: link.integrationId, kind: link.kind, externalId: link.externalId },
    runLinksResultSchema
  );
  return links;
}

export function readRunDetail(client: EngineClient, projectId: string, runId: string): Promise<RunDetail> {
  return client.request('run.detail', { projectId, runId }, runDetailSchema);
}

/** O que ainda pede atenção: espera a pessoa ou está andando. */
export const OPEN_STATES = ['awaiting_approval', 'requires_intervention', 'running', 'validating'] as const;

const TASK_LIMIT = 20;
const FEED_LIMIT = 12;

export interface ActivityFeed {
  /** O que espera a pessoa primeiro; depois o que só está andando. */
  tasks: RunItem[];
  /** As execuções mais recentes de todos os projetos, sem repetir as tarefas. */
  recent: RunItem[];
}

const waitsForPerson = (run: RunItem) => run.state === 'awaiting_approval' || run.state === 'requires_intervention';

export async function readActivityFeed(client: EngineClient): Promise<ActivityFeed> {
  const [open, latest] = await Promise.all([
    client.request('run.list', { states: [...OPEN_STATES], limit: TASK_LIMIT }, runListSchema),
    client.request('run.list', { limit: FEED_LIMIT }, runListSchema)
  ]);
  // Um diário "running" interrompido por uma queda já não é tarefa: ninguém o está executando.
  const tasks = open.runs
    .filter((run) => !run.interrupted)
    .sort((a, b) => Number(waitsForPerson(b)) - Number(waitsForPerson(a)));
  const taskIds = new Set(tasks.map((run) => run.runId));
  return { tasks, recent: latest.runs.filter((run) => !taskIds.has(run.runId)) };
}
