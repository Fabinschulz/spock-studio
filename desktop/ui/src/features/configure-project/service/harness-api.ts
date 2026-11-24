import { EngineError, followRun, type EngineClient, type FollowHandlers, type FollowOptions } from '@/shared/api';
import {
  cancelSchema,
  discoverySchema,
  planSchema,
  runSnapshotSchema,
  runStartedSchema,
  type ConfigurationPlan,
  type ConflictResolution,
  type Discovery,
  type RunEvent,
  type RunResult,
  type RunSnapshot
} from '@/shared/schema';

export type PlanInput = {
  projectId: string;
  aiEnvironment?: string;
  /** C3: as IDEs escolhidas. Diferente da declaração, o plano grava a lista em spock.yaml como mudança aprovada. */
  aiEnvironments?: string[];
  allowSpecificationReinstall?: boolean;
  /** Pede ao instalador oficial a versão mais recente da estrutura de especificação, mesmo que a instalada já seja a mais recente conhecida. */
  updateSpecification?: boolean;
  /** Pedido da tela Projeto: o motor grava na declaração do projeto (components.exclude) como mudança do plano. */
  excludeComponents?: string[];
  includeComponents?: string[];
};

export type ApplyInput = {
  projectId: string;
  planId: string;
  approvedChangeIds: string[];
  approvedCommandIds: string[];
  /** Decisão para cada conflito: manter o arquivo da pessoa ou substituir (com backup). */
  resolutions: Record<string, ConflictResolution>;
};

export type HarnessApi = {
  discover(projectId: string): Promise<Discovery>;
  plan(input: PlanInput): Promise<ConfigurationPlan>;
  apply(input: ApplyInput): Promise<{ runId: string }>;
  validate(projectId: string): Promise<{ runId: string }>;
  revert(projectId: string, runId: string): Promise<{ runId: string }>;
  snapshot(runId: string, sinceSeq?: number): Promise<RunSnapshot>;
  cancel(runId: string): Promise<{ cancelled: boolean }>;
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
};

export function createHarnessApi(client: EngineClient): HarnessApi {
  return {
    discover: (projectId) => client.request('project.discover', { projectId }, discoverySchema),
    plan: ({
      projectId,
      aiEnvironment,
      aiEnvironments,
      allowSpecificationReinstall,
      updateSpecification,
      excludeComponents,
      includeComponents
    }) =>
      client.request(
        'harness.plan',
        {
          projectId,
          ...(aiEnvironment ? { aiEnvironment } : {}),
          ...(aiEnvironments?.length ? { aiEnvironments } : {}),
          ...(allowSpecificationReinstall ? { allowSpecificationReinstall: true } : {}),
          ...(updateSpecification ? { updateSpecification: true } : {}),
          ...(excludeComponents?.length ? { excludeComponents } : {}),
          ...(includeComponents?.length ? { includeComponents } : {})
        },
        planSchema
      ),
    apply: (input) => client.request('harness.apply', input, runStartedSchema),
    validate: (projectId) => client.request('harness.validate', { projectId }, runStartedSchema),
    revert: (projectId, runId) => client.request('harness.revert', { projectId, runId }, runStartedSchema),
    snapshot: (runId, sinceSeq) =>
      client.request('run.snapshot', sinceSeq === undefined ? { runId } : { runId, sinceSeq }, runSnapshotSchema),
    cancel: (runId) => client.request('run.cancel', { runId }, cancelSchema),
    onEvent: (handler) => client.onEvent(handler)
  };
}

export type WaitHandlers = Pick<FollowHandlers<RunSnapshot>, 'started' | 'event'>;

/**
 * Inicia e acompanha uma execução até o fim pelo `followRun`: escuta antes de pedir, então o primeiro passo aparece no
 * progresso mesmo saindo antes de o pedido voltar. A releitura periódica cobre um evento de término perdido.
 */
export async function waitForRun(
  api: HarnessApi,
  start: () => Promise<string>,
  handlers: WaitHandlers,
  options: Pick<FollowOptions, 'pollMs' | 'signal'> = {}
): Promise<RunResult> {
  const snapshot = await followRun(api, start, handlers, { pollMs: 1200, ...options }).done;
  // Uma execução terminada sempre traz o resultado; sem ele, esperar mais não muda nada.
  if (snapshot.result) return snapshot.result;
  throw new EngineError('INVALID_RESPONSE', 'A execução terminou sem resultado.');
}
