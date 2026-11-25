import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import { discoverySchema, planSchema } from '@/shared/schema';

import { createJourneyStore, type JourneyStore } from '../context/journey-store';
import type { HarnessApi } from '../service';
import { useConfigureJourney } from './use-configure-journey';

const discovery = discoverySchema.parse(discoveryFixture);
const plan = planSchema.parse(planFixture);

/** API falsa em que uma execução nunca termina por si: serve para provar o que acontece com uma em andamento. */
function fakeApi(overrides: Partial<HarnessApi> = {}) {
  return {
    discover: vi.fn(async () => discovery),
    plan: vi.fn(async () => plan),
    apply: vi.fn(async () => ({ runId: 'run-1' })),
    validate: vi.fn(async () => ({ runId: 'run-2' })),
    revert: vi.fn(async () => ({ runId: 'run-3' })),
    cancel: vi.fn(async () => ({ cancelled: true })),
    // A segunda visita não entra nestes testes: o hook da jornada não lê o estado aplicado.
    status: vi.fn(),
    runs: vi.fn(async () => []),
    snapshot: vi.fn(async (runId: string) => ({
      runId,
      projectId: 'p-1',
      kind: 'apply',
      state: 'running',
      lastSeq: 0,
      events: [],
      result: null
    })),
    onEvent: vi.fn(async () => () => undefined),
    ...overrides
  } satisfies HarnessApi;
}

const journey = (
  api: HarnessApi,
  projectId: string,
  store: JourneyStore | null,
  request?: Parameters<typeof useConfigureJourney>[3]
) => renderHook(() => useConfigureJourney(api, projectId, 1, request, store));

describe('jornada Configurar · sair e voltar', () => {
  it('retoma o plano e as aprovações da pessoa, sem pedir ao motor de novo', async () => {
    const store = createJourneyStore();
    const api = fakeApi();
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    await act(() => first.result.current.actions.generatePlan());
    act(() => first.result.current.actions.toggleCommand('specification-install'));
    act(() => first.result.current.actions.goTo('review'));
    const approvedCommands = first.result.current.state.approvedCommands;
    first.unmount();

    const second = journey(api, 'p-1', store);

    expect(second.result.current.state.step).toBe('review');
    expect(second.result.current.state.plan.status).toBe('ready');
    expect(second.result.current.state.approvedCommands).toEqual(approvedCommands);
    expect(api.discover).toHaveBeenCalledTimes(1);
    expect(api.plan).toHaveBeenCalledTimes(1);
  });

  it('sem o guardador, cada visita recomeça: o comportamento de antes', async () => {
    const api = fakeApi();
    const first = journey(api, 'p-1', null);
    await act(() => first.result.current.actions.discover());
    first.unmount();

    const second = journey(api, 'p-1', null);

    expect(second.result.current.state.discovery.status).toBe('idle');
    expect(second.result.current.state.step).toBe('project');
  });

  it('a jornada de um projeto não aparece em outro', async () => {
    const store = createJourneyStore();
    const api = fakeApi();
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    first.unmount();

    const other = journey(api, 'p-2', store);

    expect(other.result.current.state.discovery.status).toBe('idle');
  });

  it('um pedido novo da tela Projeto começa do zero, sem misturar com o que ficou guardado', async () => {
    const store = createJourneyStore();
    const api = fakeApi();
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    await act(() => first.result.current.actions.generatePlan());
    first.unmount();
    const request = { exclude: [{ id: 'mariliz.agent.verifier', label: 'Agente verificador' }], include: [] };

    const second = journey(api, 'p-1', store, request);

    expect(second.result.current.state.step).toBe('project');
    expect(second.result.current.state.plan.status).toBe('idle');
    expect(second.result.current.state.componentRequest).toEqual(request);
  });

  it('concluir ou cancelar limpa o que estava guardado', async () => {
    const store = createJourneyStore();
    const api = fakeApi();
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    first.unmount();

    store.clear('p-1');
    const second = journey(api, 'p-1', store);

    expect(second.result.current.state.discovery.status).toBe('idle');
  });

  it('uma execução em andamento não é retomada: a jornada recomeça em vez de mostrar "executando" para sempre', async () => {
    const store = createJourneyStore();
    const api = fakeApi();
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    await act(() => first.result.current.actions.generatePlan());
    act(() => {
      void first.result.current.actions.apply();
    });
    await waitFor(() => expect(first.result.current.state.run).not.toBeNull());
    first.unmount();

    const second = journey(api, 'p-1', store);

    expect(second.result.current.state.run).toBeNull();
    expect(second.result.current.state.step).toBe('project');
  });

  it('um plano que estava sendo gerado volta a "ainda não pedido", sem ficar preso em "carregando"', async () => {
    const store = createJourneyStore();
    const api = fakeApi({ plan: vi.fn(() => new Promise<never>(() => undefined)) });
    const first = journey(api, 'p-1', store);
    await act(() => first.result.current.actions.discover());
    act(() => first.result.current.actions.goTo('model'));
    act(() => {
      void first.result.current.actions.generatePlan();
    });
    await waitFor(() => expect(first.result.current.state.plan.status).toBe('loading'));
    first.unmount();

    const second = journey(api, 'p-1', store);

    expect(second.result.current.state.plan.status).toBe('idle');
    expect(second.result.current.state.step).toBe('model');
  });
});
