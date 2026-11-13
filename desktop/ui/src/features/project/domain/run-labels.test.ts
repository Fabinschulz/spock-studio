import { describe, expect, it } from 'vitest';

import { runRowText } from './run-labels';

const base = { runId: 'run-1', kind: 'apply', state: 'completed', startedAt: '2026-11-04T10:00:00Z' };

describe('texto da linha de uma execução', () => {
  it('junta o desfecho e o efeito', () => {
    expect(runRowText({ ...base, changesApplied: true, reverted: false })).toBe('Concluído · alterações aplicadas');
    expect(runRowText({ ...base, kind: 'validate', changesApplied: false, reverted: false })).toBe('Concluído');
  });
});
