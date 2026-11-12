import type { EngineClient } from '@/shared/api';
import {
  runDetailSchema,
  runLinksResultSchema,
  runListSchema,
  type RunDetail,
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

/** Uma página da central de execuções. `cursor` é o que a página anterior devolveu; sem ele, a primeira. */
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

/** O resumo do motor, pedindo uma execução só: o que anda e o que espera aprovação, para o indicador do menu. */
export async function readRunActivity(client: EngineClient): Promise<RunActivity> {
  const { summary } = await client.request('run.list', { limit: 1 }, runListSchema);
  return { running: summary.running, awaitingApproval: summary.awaitingApproval };
}

/** Liga a execução a um item já sincronizado do projeto, pelo número ou pela chave. Devolve as ligações depois da mudança. */
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

/** Só leitura: abrir o detalhe nunca dispara, reverte nem valida nada. */
export function readRunDetail(client: EngineClient, projectId: string, runId: string): Promise<RunDetail> {
  return client.request('run.detail', { projectId, runId }, runDetailSchema);
}
