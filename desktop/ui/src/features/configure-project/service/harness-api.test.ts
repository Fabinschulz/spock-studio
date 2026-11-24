import { describe, expect, it, vi } from 'vitest';

import type { RunSnapshot } from '@/shared/schema';

import { waitForRun, type HarnessApi } from './harness-api';

function apiWith(snapshot: () => RunSnapshot): HarnessApi {
  return {
    snapshot: vi.fn(async () => snapshot()),
    onEvent: vi.fn(async () => () => undefined)
  } as unknown as HarnessApi;
}

const base: RunSnapshot = {
  runId: 'run-1',
  projectId: 'p-1',
  kind: 'apply',
  state: 'running',
  lastSeq: -1,
  events: [],
  result: null
};

const start = async () => 'run-1';

describe('waitForRun', () => {
  it('execução terminada sem resultado falha em vez de consultar para sempre', async () => {
    const api = apiWith(() => ({ ...base, state: 'failed' }));

    await expect(waitForRun(api, start, {}, { pollMs: 5 })).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
    expect(api.snapshot).toHaveBeenCalledTimes(1);
  });

  it('interromper durante a espera encerra na hora', async () => {
    const controller = new AbortController();
    const api = apiWith(() => base);
    const waiting = waitForRun(api, start, {}, { pollMs: 60_000, signal: controller.signal });

    await vi.waitFor(() => expect(api.snapshot).toHaveBeenCalledTimes(1));
    controller.abort();

    await expect(waiting).rejects.toMatchObject({ code: 'ABORTED' });
  });

  it('escuta antes de pedir: o passo publicado antes de o pedido voltar chega ao progresso', async () => {
    let publish: ((type: string) => void) | null = null;
    let lastSeq = -1;
    const api = {
      onEvent: vi.fn(async (handler: Parameters<HarnessApi['onEvent']>[0]) => {
        publish = (type) => {
          lastSeq += 1;
          handler({ v: 1, kind: 'event', runId: 'run-1', seq: lastSeq, type, data: { id: 'backup' } });
        };
        return () => undefined;
      }),
      snapshot: vi.fn(async () => ({
        ...base,
        state: 'completed',
        lastSeq,
        result: { outcome: 'completed' } as unknown as RunSnapshot['result']
      }))
    } as unknown as HarnessApi;
    const seen: string[] = [];
    const ids: string[] = [];

    await waitForRun(
      api,
      async () => {
        publish?.('step.started');
        return 'run-1';
      },
      { started: (id) => ids.push(id), event: (event) => seen.push(event.type) }
    );

    expect(ids).toEqual(['run-1']);
    expect(seen).toEqual(['step.started']);
  });
});
