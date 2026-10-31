import { describe, expect, it } from 'vitest';

import { toWorkflowStartInput } from './form-schema';

describe('toWorkflowStartInput', () => {
  it('monta o input do motor sem chaves falsas', () => {
    expect(
      toWorkflowStartInput({ goal: '  objetivo  ', impact: true, requiresApproval: false })
    ).toEqual({ goal: 'objetivo', impact: true });
    expect(toWorkflowStartInput({ goal: 'x', impact: false, requiresApproval: true })).toEqual({
      goal: 'x',
      requiresApproval: true
    });
  });
});
