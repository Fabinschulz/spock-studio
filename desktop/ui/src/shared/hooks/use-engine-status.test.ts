import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { parseEngineReport, useEngineStatus } from './use-engine-status';

const invoke = vi.fn();
const listen = vi.fn();
vi.mock('@tauri-apps/api/core', () => ({ invoke: (...args: unknown[]) => invoke(...args) }));
vi.mock('@tauri-apps/api/event', () => ({ listen: (...args: unknown[]) => listen(...args) }));

const tauri = window as unknown as Record<string, unknown>;

afterEach(() => {
  delete tauri.__TAURI_INTERNALS__;
  invoke.mockReset();
  listen.mockReset();
});

describe('parseEngineReport', () => {
  it('aceita o texto antigo e o relatório com o runtime de IA', () => {
    expect(parseEngineReport('ready')).toEqual({ status: 'ready', aiRuntime: 'unknown' });
    expect(parseEngineReport({ engine: 'crashed', aiRuntime: 'stopped' })).toEqual({
      status: 'crashed',
      aiRuntime: 'stopped'
    });
  });

  it('valor fora do vocabulário nunca vira "pronto"', () => {
    expect(parseEngineReport('quase-pronto')).toEqual({ status: 'unknown', aiRuntime: 'unknown' });
    expect(parseEngineReport({ engine: 'ready', aiRuntime: 42 })).toEqual({ status: 'ready', aiRuntime: 'unknown' });
    expect(parseEngineReport(null)).toEqual({ status: 'unknown', aiRuntime: 'unknown' });
    expect(parseEngineReport(7)).toEqual({ status: 'unknown', aiRuntime: 'unknown' });
  });
});

describe('useEngineStatus', () => {
  it('desmontar antes de a escuta ligar não deixa a escuta viva', async () => {
    tauri.__TAURI_INTERNALS__ = {};
    const stop = vi.fn();
    let bind: (stop: () => void) => void = () => undefined;
    listen.mockReturnValue(new Promise<() => void>((resolve) => (bind = resolve)));

    const { unmount } = renderHook(() => useEngineStatus());
    await waitFor(() => expect(listen).toHaveBeenCalled());
    unmount();
    await act(async () => bind(stop));

    expect(stop).toHaveBeenCalledTimes(1);
    expect(invoke).not.toHaveBeenCalled();
  });

  it('desliga a escuta ao desmontar', async () => {
    tauri.__TAURI_INTERNALS__ = {};
    const stop = vi.fn();
    listen.mockResolvedValue(stop);
    invoke.mockResolvedValue('ready');

    const { result, unmount } = renderHook(() => useEngineStatus());
    await waitFor(() => expect(result.current.status).toBe('ready'));
    unmount();

    expect(stop).toHaveBeenCalledTimes(1);
  });
});
