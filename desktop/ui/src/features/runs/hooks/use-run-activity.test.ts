import type { EngineClient } from '@/shared/api';
import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ACTIVITY_REFRESH_MS, useRunActivity } from './use-run-activity';

type Summary = { total: number; running: number; awaitingApproval: number };

function engine(next: () => Summary | Error) {
  const request = vi.fn(async (_method: string, _params: unknown, schema: { parse(value: unknown): unknown }) => {
    const value = next();
    if (value instanceof Error) throw value;
    return schema.parse({ runs: [], nextCursor: null, summary: value });
  });
  return { client: { request, onEvent: vi.fn() } as unknown as EngineClient, request };
}

beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
afterEach(() => vi.useRealTimers());

describe('useRunActivity', () => {
  it('lê ao ligar, pedindo uma execução só, e devolve o resumo', async () => {
    const { client, request } = engine(() => ({ total: 5, running: 2, awaitingApproval: 1 }));
    const { result } = renderHook(() => useRunActivity(client, true, '/'));

    await waitFor(() => expect(result.current).toEqual({ running: 2, awaitingApproval: 1 }));
    expect(request).toHaveBeenCalledWith('run.list', { limit: 1 }, expect.anything());
  });

  it('relê a cada intervalo', async () => {
    let running = 1;
    const { client, request } = engine(() => ({ total: 5, running, awaitingApproval: 0 }));
    const { result } = renderHook(() => useRunActivity(client, true, '/'));
    await waitFor(() => expect(result.current?.running).toBe(1));

    running = 4;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACTIVITY_REFRESH_MS);
    });

    await waitFor(() => expect(result.current?.running).toBe(4));
    expect(request.mock.calls.length).toBeGreaterThanOrEqual(2);
  });

  it('relê quando a tela muda', async () => {
    const { client, request } = engine(() => ({ total: 1, running: 1, awaitingApproval: 0 }));
    const { rerender } = renderHook(({ key }) => useRunActivity(client, true, key), { initialProps: { key: '/a' } });
    await waitFor(() => expect(request).toHaveBeenCalledTimes(1));

    rerender({ key: '/b' });

    await waitFor(() => expect(request).toHaveBeenCalledTimes(2));
  });

  it('sem motor pronto não pergunta e não mostra número velho', async () => {
    const { client, request } = engine(() => ({ total: 1, running: 1, awaitingApproval: 0 }));
    const { result, rerender } = renderHook(({ on }) => useRunActivity(client, on, '/'), {
      initialProps: { on: true }
    });
    await waitFor(() => expect(result.current).not.toBeNull());

    rerender({ on: false });

    expect(result.current).toBeNull();
    const calls = request.mock.calls.length;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACTIVITY_REFRESH_MS * 2);
    });
    expect(request.mock.calls.length).toBe(calls);
  });

  it('uma leitura que falha deixa o último valor em vez de apagar o aviso', async () => {
    let fail = false;
    const { client } = engine(() => (fail ? new Error('motor fora') : { total: 2, running: 0, awaitingApproval: 2 }));
    const { result } = renderHook(() => useRunActivity(client, true, '/'));
    await waitFor(() => expect(result.current?.awaitingApproval).toBe(2));

    fail = true;
    await act(async () => {
      await vi.advanceTimersByTimeAsync(ACTIVITY_REFRESH_MS);
    });

    expect(result.current).toEqual({ running: 0, awaitingApproval: 2 });
  });
});
