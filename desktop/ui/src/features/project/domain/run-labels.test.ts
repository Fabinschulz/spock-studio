import { describe, expect, it } from 'vitest';

import { runEffectLabel, runKindLabel, runRowText } from './run-labels';

const base = { runId: 'run-1', kind: 'apply', state: 'completed', startedAt: '2026-11-04T10:00:00Z' };

describe('rótulos das execuções', () => {
  it('tipo desconhecido aparece como veio', () => {
    expect(runKindLabel('apply')).toBe('Configuração aplicada');
    expect(runKindLabel('novo-tipo')).toBe('novo-tipo');
  });

  it('só diz o que a execução deixou no projeto quando é verdade', () => {
    expect(runEffectLabel({ kind: 'apply', changesApplied: true, reverted: false })).toBe('alterações aplicadas');
    expect(runEffectLabel({ kind: 'apply', changesApplied: true, reverted: true })).toBe('revertida depois');
    expect(runEffectLabel({ kind: 'validate', changesApplied: false, reverted: false })).toBe('');
  });

  it('junta o desfecho e o efeito', () => {
    expect(runRowText({ ...base, changesApplied: true, reverted: false })).toBe('Concluído · alterações aplicadas');
    expect(runRowText({ ...base, kind: 'validate', changesApplied: false, reverted: false })).toBe('Concluído');
  });
});
