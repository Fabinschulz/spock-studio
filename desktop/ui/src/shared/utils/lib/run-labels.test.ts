import { describe, expect, it } from 'vitest';

import { runEffectLabel, runKindLabel } from './run-labels';

describe('rótulos das execuções', () => {
  it('tipo desconhecido aparece como veio', () => {
    expect(runKindLabel('apply')).toBe('Configuração aplicada');
    expect(runKindLabel('novo-tipo')).toBe('novo-tipo');
    expect(runKindLabel('workflow')).toBe('Workflow');
    expect(runKindLabel('assistant')).toBe('Pergunta ao assistente');
    expect(runKindLabel('investigation')).toBe('Investigação de gestão');
  });

  it('só diz o que a execução deixou no projeto quando é verdade', () => {
    expect(runEffectLabel({ kind: 'apply', changesApplied: true, reverted: false })).toBe('alterações aplicadas');
    expect(runEffectLabel({ kind: 'apply', changesApplied: true, reverted: true })).toBe('revertida depois');
    expect(runEffectLabel({ kind: 'validate', changesApplied: false, reverted: false })).toBe('');
  });
});
