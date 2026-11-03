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
  lastSeq: 0,
  events: [],
  result: null
};

describe('waitForRun', () => {
  it('execução terminada sem resultado falha em vez de consultar para sempre', async () => {
    const api = apiWith(() => ({ ...base, state: 'failed' }));

    await expect(waitForRun(api, 'run-1', () => undefined, { pollMs: 5 })).rejects.toMatchObject({
      code: 'INVALID_RESPONSE'
    });
    expect(api.snapshot).toHaveBeenCalledTimes(1);
  });

  it('interromper durante a espera encerra na hora', async () => {
    const controller = new AbortController();
    const api = apiWith(() => base);
    const waiting = waitForRun(api, 'run-1', () => undefined, { pollMs: 60_000, signal: controller.signal });

    await vi.waitFor(() => expect(api.snapshot).toHaveBeenCalledTimes(1));
    controller.abort();

    await expect(waiting).rejects.toMatchObject({ code: 'ABORTED' });
  });
});
