import type { EngineClient } from '@/shared/api';
import { runDetailSchema, runListSchema, type RunDetail, type RunList } from '@/shared/schema';

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

/** Só leitura: abrir o detalhe nunca dispara, reverte nem valida nada. */
export function readRunDetail(client: EngineClient, projectId: string, runId: string): Promise<RunDetail> {
  return client.request('run.detail', { projectId, runId }, runDetailSchema);
}
