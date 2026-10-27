import {
  cancelSchema,
  discoverySchema,
  planSchema,
  runSnapshotSchema,
  runStartedSchema,
  runsListSchema,
  type ConfigurationPlan,
  type Discovery,
  type RunEvent,
  type RunResult,
  type RunSnapshot,
  type RunSummary
} from '@/shared/schemas';

import { EngineError, type EngineClient } from './engine-client';

export type PlanInput = {
  projectId: string;
  aiEnvironment?: string;
  allowTlcForceReinstall?: boolean;
  /** Pede ao instalador oficial a versão mais recente do TLC, mesmo que a instalada já seja a mais recente conhecida. */
  updateTlc?: boolean;
};

export type ApplyInput = {
  projectId: string;
  planId: string;
  approvedChangeIds: string[];
  approvedCommandIds: string[];
  /** Decisão para cada conflito: manter o arquivo da pessoa ou substituir (com backup). */
  resolutions: Record<string, 'keep' | 'overwrite'>;
};

/** Os casos de uso da jornada, como o motor os expõe. A interface só chama isto; nenhuma regra mora aqui. */
export type HarnessApi = {
  discover(projectId: string): Promise<Discovery>;
  plan(input: PlanInput): Promise<ConfigurationPlan>;
  apply(input: ApplyInput): Promise<{ runId: string }>;
  validate(projectId: string): Promise<{ runId: string }>;
  revert(projectId: string, runId: string): Promise<{ runId: string }>;
  snapshot(runId: string): Promise<RunSnapshot>;
  cancel(runId: string): Promise<{ cancelled: boolean }>;
  runs(projectId: string): Promise<RunSummary[]>;
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
};

export function createHarnessApi(client: EngineClient): HarnessApi {
  return {
    discover: (projectId) => client.request('project.discover', { projectId }, discoverySchema),
    plan: ({ projectId, aiEnvironment, allowTlcForceReinstall, updateTlc }) =>
      client.request(
        'harness.plan',
        {
          projectId,
          ...(aiEnvironment ? { aiEnvironment } : {}),
          ...(allowTlcForceReinstall ? { allowTlcForceReinstall: true } : {}),
          ...(updateTlc ? { updateTlc: true } : {})
        },
        planSchema
      ),
    apply: (input) => client.request('harness.apply', input, runStartedSchema),
    validate: (projectId) => client.request('harness.validate', { projectId }, runStartedSchema),
    revert: (projectId, runId) => client.request('harness.revert', { projectId, runId }, runStartedSchema),
    snapshot: (runId) => client.request('run.snapshot', { runId }, runSnapshotSchema),
    cancel: (runId) => client.request('run.cancel', { runId }, cancelSchema),
    runs: async (projectId) => (await client.request('harness.runs', { projectId }, runsListSchema)).runs,
    onEvent: (handler) => client.onEvent(handler)
  };
}

const TERMINAL = ['completed', 'completed_with_limitations', 'failed', 'requires_intervention', 'cancelled'];

export type WaitOptions = { pollMs?: number; signal?: AbortSignal };

/**
 * Acompanha uma execução até o fim. Eventos dão o progresso em tempo real; a consulta periódica ao snapshot
 * garante o resultado mesmo se um evento se perder ou a execução terminar antes da escuta começar.
 */
export async function waitForRun(
  api: HarnessApi,
  runId: string,
  onEvent: (event: RunEvent) => void,
  { pollMs = 1200, signal }: WaitOptions = {}
): Promise<RunResult> {
  const unlisten = await api.onEvent((event) => {
    if (event.runId === runId) onEvent(event);
  });

  try {
    for (;;) {
      if (signal?.aborted) throw new EngineError('ABORTED', 'O acompanhamento foi interrompido.');
      const snapshot = await api.snapshot(runId);
      if (TERMINAL.includes(snapshot.state) && snapshot.result) return snapshot.result;
      await new Promise((resolve) => setTimeout(resolve, pollMs));
    }
  } finally {
    unlisten();
  }
}
