import { runEventSchema, type RunEvent } from '@/shared/schema';
import type { ZodType } from 'zod';

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

/** Fala com o motor .NET. A única implementação real passa pelos comandos do host Tauri. */
export type EngineClient = {
  request<T>(method: string, params: Record<string, unknown>, schema: ZodType<T>): Promise<T>;
  onEvent(handler: (event: RunEvent) => void): Promise<() => void>;
};

let counter = 0;

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
    if (!parsed.success) throw new EngineError('INVALID_RESPONSE', 'O motor respondeu num formato inesperado.');
    return parsed.data;
  },

  async onEvent(handler) {
    const { listen } = await import('@tauri-apps/api/event');
    return listen<unknown>('engine_event', (event) => {
      const parsed = runEventSchema.safeParse(event.payload);
      if (parsed.success) handler(parsed.data);
    });
  }
};
