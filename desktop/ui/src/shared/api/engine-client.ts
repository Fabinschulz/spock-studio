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

/** Mensagem para a pessoa: a do motor quando ele explica o erro; senão, o texto de quem chamou. */
export function errorMessage(error: unknown, fallback: string): string {
  return error instanceof EngineError ? error.message : fallback;
}

/**
 * Os métodos que a interface pode pedir ao motor. É a mesma lista que o host Rust deixa passar (`FRONTEND_METHODS` em
 * `desktop/host/src-tauri/src/protocol.rs`): um método fora dela seria recusado, então o compilador já o recusa aqui.
 */
export const ENGINE_METHODS = [
  'project.discover',
  'harness.plan',
  'harness.apply',
  'harness.validate',
  'harness.revert',
  'harness.runs',
  'run.snapshot',
  'run.cancel',
  'run.list',
  'run.detail',
  'workflow.start',
  'workflow.resume',
  'model.status',
  'model.save',
  'model.clear',
  'model.complete',
  'integration.catalog',
  'connection.list',
  'connection.create',
  'connection.test',
  'connection.rotate',
  'connection.disconnect',
  'connection.remove',
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
  'management.evidence',
  'management.ask',
  'engineering.profile',
  'engineering.analyze',
  'engineering.knowledge',
  'engineering.harness',
  'engineering.revisions'
] as const;

export type EngineMethod = (typeof ENGINE_METHODS)[number];

/** Fala com o motor .NET. A única implementação real passa pelos comandos do host Tauri. */
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

function toEngineError(error: unknown): EngineError {
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
