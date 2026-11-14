import { toEngineError } from '@/shared/api';
import { z } from 'zod';

const eventSchema = z.discriminatedUnion('kind', [
  z.object({ kind: z.literal('output'), data: z.string() }),
  z.object({ kind: z.literal('exit'), code: z.number().nullable() })
]);

const openedSchema = z.object({ sessionId: z.string(), shell: z.string() });

export type TerminalEvent = z.infer<typeof eventSchema>;
export type TerminalOpened = z.infer<typeof openedSchema>;
export type TerminalSize = { cols: number; rows: number };

/** O host abre o terminal na pasta do projeto pelo id do recente: a tela nunca manda um caminho. */
export interface TerminalApi {
  open(recentId: string, size: TerminalSize, onEvent: (event: TerminalEvent) => void): Promise<TerminalOpened>;
  write(sessionId: string, data: string): Promise<void>;
  resize(sessionId: string, size: TerminalSize): Promise<void>;
  close(sessionId: string): Promise<void>;
}

async function call<T>(command: string, args: Record<string, unknown>): Promise<T> {
  const { invoke } = await import('@tauri-apps/api/core');
  try {
    return await invoke<T>(command, args);
  } catch (error) {
    throw toEngineError(error);
  }
}

export const tauriTerminalApi: TerminalApi = {
  async open(recentId, { cols, rows }, onEvent) {
    const { Channel } = await import('@tauri-apps/api/core');
    const channel = new Channel<unknown>();
    channel.onmessage = (message) => {
      const parsed = eventSchema.safeParse(message);
      if (parsed.success) onEvent(parsed.data);
    };
    return openedSchema.parse(await call('terminal_open', { recentId, cols, rows, onEvent: channel }));
  },
  write: (sessionId, data) => call('terminal_write', { sessionId, data }),
  resize: (sessionId, { cols, rows }) => call('terminal_resize', { sessionId, cols, rows }),
  close: (sessionId) => call('terminal_close', { sessionId })
};
