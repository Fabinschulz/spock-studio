import { describe, expect, it, vi } from 'vitest';

import type { EngineClient } from '@/shared/api';

import { createHarnessApi } from '../service';
import { componentRequestState, EMPTY_COMPONENT_REQUEST, readComponentRequest } from './component-request';

describe('pedido de componentes pela rota', () => {
  it('lê só o formato esperado; qualquer outra coisa vira pedido vazio', () => {
    const item = { id: 'mariliz.hook.policy', label: 'Hook de política' };
    expect(readComponentRequest(componentRequestState({ exclude: [item] }))).toEqual({ exclude: [item], include: [] });
    expect(readComponentRequest({ componentRequest: { exclude: [{ id: 1 }, item], include: 'x' } })).toEqual({
      exclude: [item],
      include: []
    });
    expect(readComponentRequest(null)).toBe(EMPTY_COMPONENT_REQUEST);
    expect(readComponentRequest({ outra: true })).toBe(EMPTY_COMPONENT_REQUEST);
  });

  it('o plano só manda as listas ao motor quando há pedido', async () => {
    const request = vi.fn(async () => ({}));
    const api = createHarnessApi({ request } as unknown as EngineClient);

    await api.plan({ projectId: 'p-1', excludeComponents: ['mariliz.agent.verifier'], includeComponents: [] });
    await api.plan({ projectId: 'p-1' });

    expect(request).toHaveBeenNthCalledWith(
      1,
      'harness.plan',
      { projectId: 'p-1', excludeComponents: ['mariliz.agent.verifier'] },
      expect.anything()
    );
    expect(request).toHaveBeenNthCalledWith(2, 'harness.plan', { projectId: 'p-1' }, expect.anything());
  });
});
