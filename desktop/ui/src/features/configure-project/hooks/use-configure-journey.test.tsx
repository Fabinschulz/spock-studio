import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import applyResult from '@contracts/fixtures/golden/apply-result.json';
import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import { EngineError } from '@/shared/api';
import { discoverySchema, planSchema, runResultSchema, type RunEvent } from '@/shared/schema';

import type { HarnessApi } from '../service';
import { useConfigureJourney } from './use-configure-journey';

const discovery = discoverySchema.parse(discoveryFixture);
const plan = planSchema.parse(planFixture);
const result = runResultSchema.parse(applyResult);

/** API falsa: a execução termina na segunda leitura e emite um evento no caminho. */
function fakeApi(overrides: Partial<HarnessApi> = {}) {
  let listener: ((event: RunEvent) => void) | null = null;
  let reads = 0;
  const api = {
    discover: vi.fn(async () => discovery),
    plan: vi.fn(async () => plan),
    apply: vi.fn(async () => ({ runId: 'run-1' })),
    validate: vi.fn(async () => ({ runId: 'run-2' })),
    revert: vi.fn(async () => ({ runId: 'run-3' })),
    cancel: vi.fn(async () => ({ cancelled: true })),
    snapshot: vi.fn(async (runId: string) => {
      reads += 1;
      if (reads === 1)
        listener?.({ v: 1, kind: 'event', runId, seq: 0, type: 'run.state', data: { state: 'validating' } });
      const done = reads > 1;
      return {
        runId,
        projectId: 'p-1',
        kind: 'apply',
        state: done ? 'completed' : 'running',
        lastSeq: 0,
        events: [],
        result: done ? result : null
      };
    }),
    onEvent: vi.fn(async (handler: (event: RunEvent) => void) => {
      listener = handler;
      return () => {
        listener = null;
      };
    }),
    ...overrides
  } satisfies HarnessApi;
  return api;
}

describe('useConfigureJourney', () => {
  it('descobre, planeja, aplica com as aprovações da pessoa e chega ao resultado', async () => {
    const api = fakeApi();
    const { result: hook } = renderHook(() => useConfigureJourney(api, 'p-1', 1));

    await act(() => hook.current.actions.discover());
    expect(hook.current.state.discovery.status).toBe('ready');
    expect(hook.current.state.aiEnvironment).toBe('claude-code');

    await act(() => hook.current.actions.generatePlan());
    expect(api.plan).toHaveBeenCalledWith({
      projectId: 'p-1',
      aiEnvironment: 'claude-code',
      allowSpecificationReinstall: false,
      updateSpecification: false
    });
    expect(hook.current.state.step).toBe('plan');

    act(() => hook.current.actions.toggleCommand('specification-install'));
    await act(() => hook.current.actions.apply());

    expect(api.apply).toHaveBeenCalledWith(
      expect.objectContaining({ projectId: 'p-1', planId: plan.planId, approvedCommandIds: ['specification-install'] })
    );
    await waitFor(() => expect(hook.current.state.step).toBe('result'));
    expect(hook.current.state.results.apply).toEqual(result);
    expect(hook.current.state.run).toBeNull();
  });

  it('erro do motor aparece com a mensagem dele; erro desconhecido vira mensagem genérica', async () => {
    const api = fakeApi({
      discover: vi.fn(async () => {
        throw new EngineError('PROJECT_NOT_AUTHORIZED', 'O projeto não foi autorizado.');
      }),
      plan: vi.fn(async () => {
        throw new Error('quebrou');
      })
    });
    const { result: hook } = renderHook(() => useConfigureJourney(api, 'p-1', 1));

    await act(() => hook.current.actions.discover());
    expect(hook.current.state.discovery).toEqual({ status: 'error', message: 'O projeto não foi autorizado.' });
    await act(() => hook.current.actions.generatePlan());
    expect(hook.current.state.plan).toEqual({ status: 'error', message: 'Algo inesperado aconteceu. Tente de novo.' });
  });

  it('cancelar pede ao motor o cancelamento da execução em andamento', async () => {
    const api = fakeApi({
      snapshot: vi.fn(async (runId: string) => ({
        runId,
        projectId: 'p-1',
        kind: 'validate',
        state: 'running',
        lastSeq: 0,
        events: [],
        result: null
      }))
    });
    const { result: hook, unmount } = renderHook(() => useConfigureJourney(api, 'p-1', 1));

    // A execução fica em andamento: a ação não termina, então não se espera por ela.
    act(() => {
      void hook.current.actions.validate();
    });
    await waitFor(() => expect(hook.current.state.run?.runId).toBe('run-2'));
    await act(() => hook.current.actions.cancelRun());

    expect(api.cancel).toHaveBeenCalledWith('run-2');
    unmount();
  });
});
