import { runEventSchema, type RunEvent } from '@/shared/schema';
import type { ZodError, ZodType } from 'zod';

export class EngineError extends Error {
  constructor(
    readonly code: string,
    message: string
  ) {
    super(message);
    this.name = 'EngineError';
  }
}

export function errorMessage(error: unknown, fallback: string): string {
  return error instanceof EngineError ? error.message : fallback;
}

/**
 * Os métodos que a interface pode pedir ao motor. É a mesma lista que o host Rust deixa passar (`FRONTEND_METHODS` em
 * `desktop/host/src-tauri/src/protocol.rs`): um método fora dela seria recusado, então o compilador já o recusa aqui.
 */
export const ENGINE_METHODS = [
  'project.discover',
  'project.architecture',
  'harness.plan',
  'harness.apply',
  'harness.validate',
  'harness.revert',
  'harness.runs',
  'run.snapshot',
  'run.cancel',
  'run.list',
  'run.detail',
  'run.link',
  'run.unlink',
  'run.linked',
  'workflow.start',
  'workflow.resume',
  'model.status',
  'model.save',
  'model.remove',
  'model.default',
  'model.connection',
  'model.test',
  'model.purpose',
  'model.budget',
  'integration.catalog',
  'connection.list',
  'connection.create',
  'connection.test',
  'connection.rotate',
  'connection.disconnect',
  'connection.remove',
  'connection.reach',
  'integration.list',
  'integration.link',
  'integration.configure',
  'integration.unlink',
  'integration.sync',
  'integration.history',
  'management.portfolio',
  'management.metrics',
  'management.forecast',
  'management.risks',
  'management.riskhistory',
  'management.report',
  'management.milestones',
  'management.evidence',
  'management.ask',
  'management.investigate',
  'management.portfolioevidence',
  'management.portfolioask',
  'decision.record',
  'decision.list',
  'decision.get',
  'modernization.plan',
  'modernization.decide',
  'engineering.profile',
  'engineering.analyze',
  'engineering.knowledge',
  'engineering.harness',
  'engineering.revisions',
  'engineering.catalog',
  'engineering.mcp',
  'engineering.policy',
  'timeline.read',
  'audit.read',
  'aitool.list',
  'agent.run',
  'ide.connection',
  'context.review',
  'security.scan',
  'security.plan',
  'security.fix',
  'idea.draft',
  'idea.plan',
  'idea.save',
  'project.design',
  'project.scaffold',
  'cloud.catalog',
  'cloud.list',
  'cloud.create',
  'cloud.validate',
  'cloud.permissions',
  'cloud.remove',
  'cloud.links',
  'cloud.link',
  'cloud.configure',
  'cloud.unlink',
  'cloud.read',
  'iac.runtime.status',
  'iac.runtime.offer',
  'iac.runtime.install',
  'iac.runtime.decline',
  'iac.runtime.remove',
  'iac.runtime.adopt',
  'iac.runtime.pin',
  'iac.layout',
  'iac.validate',
  'iac.plan',
  'iac.operations',
  'iac.operation',
  'iac.apply',
  'iac.state',
  'iac.state.resources',
  'iac.state.unlock',
  'iac.bootstrap.plan',
  'iac.bootstrap.save',
  'iac.architect.draft',
  'iac.architect.proposal',
  'iac.generate.options',
  'iac.generate.plan',
  'iac.generate.save',
  'iac.drift',
  'iac.cost.estimate',
  'iac.cost.plan',
  'iac.drift.schedule',
  'iac.drift.schedule.set',
  'boot.start',
  'boot.project',
  'boot.resume',
  'boot.list',
  'boot.detail',
  'boot.capabilities',
  'quality.review.start',
  'quality.review.list',
  'quality.review.detail'
] as const;

export type EngineMethod = (typeof ENGINE_METHODS)[number];

export type EngineClient = {
  request<T>(method: EngineMethod, params: Record<string, unknown>, schema: ZodType<T>): Promise<T>;
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
};

let counter = 0;

/**
 * O que o motor mandou e a interface não entende é desvio de contrato: fica registrado para quem desenvolve ver onde,
 * sem o valor recebido (pode ter conteúdo do projeto). A pessoa vê só a mensagem genérica.
 */
function warnContractDrift(what: string, error: ZodError) {
  const where = error.issues.map((issue) => `${issue.path.join('.') || '(raiz)'}: ${issue.message}`);
  console.warn(`[motor] ${what} fora do contrato`, where);
}

/** O host responde erro como `{ code, message }`; o resto vira uma falha genérica de comunicação. */
export function toEngineError(error: unknown): EngineError {
  if (typeof error === 'object' && error !== null && 'code' in error && 'message' in error) {
    return new EngineError(String(error.code), String(error.message));
  }
  return new EngineError('UNKNOWN', 'Não foi possível falar com o motor.');
}

export const tauriEngineClient: EngineClient = {
  async request(method, params, schema) {
    const { invoke } = await import('@tauri-apps/api/core');
    const envelope = { v: 1, kind: 'request', id: `ui-${++counter}`, method, params };

    let raw: unknown;
    try {
      raw = await invoke('engine_request', { envelope });
    } catch (error) {
      throw toEngineError(error);
    }

    const parsed = schema.safeParse(raw);
    if (!parsed.success) {
      warnContractDrift(`resposta de ${method}`, parsed.error);
      throw new EngineError('INVALID_RESPONSE', 'O motor respondeu num formato inesperado.');
    }
    return parsed.data;
  },

  async onEvent(handler) {
    const { listen } = await import('@tauri-apps/api/event');
    return listen<unknown>('engine_event', (event) => {
      const parsed = runEventSchema.safeParse(event.payload);
      if (parsed.success) handler(parsed.data);
      else warnContractDrift('evento', parsed.error);
    });
  }
};
