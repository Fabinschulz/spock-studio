import { describe, expect, it } from 'vitest';

import { engineeringRequestFormDefaults, toWorkflowStartInput } from './form-schema';

describe('toWorkflowStartInput', () => {
  it('monta o input do motor sem chaves falsas', () => {
    expect(
      toWorkflowStartInput({ goal: '  objetivo  ', impact: true, requiresApproval: false, useModel: false })
    ).toEqual({
      goal: 'objetivo',
      impact: true
    });
    expect(toWorkflowStartInput({ goal: 'x', impact: false, requiresApproval: true, useModel: false })).toEqual({
      goal: 'x',
      requiresApproval: true
    });
  });

  it('o modelo só vai no pedido quando a pessoa marcou, e o padrão é sem modelo', () => {
    expect(engineeringRequestFormDefaults.useModel).toBe(false);
    expect(toWorkflowStartInput({ goal: 'x', impact: false, requiresApproval: false, useModel: true })).toEqual({
      goal: 'x',
      useModel: true
    });
    expect(toWorkflowStartInput({ ...engineeringRequestFormDefaults, goal: 'x' })).not.toHaveProperty('useModel');
  });
});
