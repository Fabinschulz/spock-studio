import { EngineError, type EngineClient } from '@/shared/api';
import {
  cancelSchema,
  discoverySchema,
  isTerminalState,
  planSchema,
  runSnapshotSchema,
  runStartedSchema,
  runsListSchema,
  type ConfigurationPlan,
  type ConflictResolution,
  type Discovery,
  type RunEvent,
  type RunResult,
  type RunSnapshot,
  type RunSummary
} from '@/shared/schema';

export type PlanInput = {
  projectId: string;
  aiEnvironment?: string;
  allowSpecificationReinstall?: boolean;
  /** Pede ao instalador oficial a versão mais recente da estrutura de especificação, mesmo que a instalada já seja a mais recente conhecida. */
  updateSpecification?: boolean;
};

export type ApplyInput = {
  projectId: string;
  planId: string;
  approvedChangeIds: string[];
  approvedCommandIds: string[];
  /** Decisão para cada conflito: manter o arquivo da pessoa ou substituir (com backup). */
  resolutions: Record<string, ConflictResolution>;
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
    plan: ({ projectId, aiEnvironment, allowSpecificationReinstall, updateSpecification }) =>
      client.request(
        'harness.plan',
        {
          projectId,
          ...(aiEnvironment ? { aiEnvironment } : {}),
          ...(allowSpecificationReinstall ? { allowSpecificationReinstall: true } : {}),
          ...(updateSpecification ? { updateSpecification: true } : {})
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
      if (signal?.aborted) throw aborted();
      const snapshot = await api.snapshot(runId);
      if (isTerminalState(snapshot.state)) {
        // Uma execução terminada sempre traz o resultado; sem ele, esperar mais não muda nada.
        if (snapshot.result) return snapshot.result;
        throw new EngineError('INVALID_RESPONSE', 'A execução terminou sem resultado.');
      }
      await sleep(pollMs, signal);
    }
  } finally {
    unlisten();
  }
}

function aborted(): EngineError {
  return new EngineError('ABORTED', 'O acompanhamento foi interrompido.');
}

/** Espera que termina na hora em que o acompanhamento é interrompido, sem aguardar o próximo ciclo. */
function sleep(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      signal?.removeEventListener('abort', onAbort);
      resolve();
    }, ms);
    function onAbort() {
      clearTimeout(timer);
      reject(aborted());
    }
    signal?.addEventListener('abort', onAbort, { once: true });
  });
}
