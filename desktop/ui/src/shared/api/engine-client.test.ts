import { afterEach, describe, expect, it, vi } from 'vitest';
import { z } from 'zod';

import { EngineError, tauriEngineClient } from './engine-client';

const invoke = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: vi.fn() }));

afterEach(() => {
  invoke.mockReset();
  vi.restoreAllMocks();
});

describe('tauriEngineClient', () => {
  it('resposta fora do contrato vira INVALID_RESPONSE e registra onde, sem o valor recebido', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    invoke.mockResolvedValue({ configured: 'segredo-que-nao-pode-vazar' });

    const request = tauriEngineClient.request('model.status', {}, z.object({ configured: z.boolean() }));

    await expect(request).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
    await expect(request).rejects.toBeInstanceOf(EngineError);
    expect(warn).toHaveBeenCalled();
    expect(JSON.stringify(warn.mock.calls)).toContain('configured');
    expect(JSON.stringify(warn.mock.calls)).not.toContain('segredo-que-nao-pode-vazar');
  });
});
