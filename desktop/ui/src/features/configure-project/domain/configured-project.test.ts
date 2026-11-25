import type { HarnessStatus, RunSummary } from '@/shared/schema';
import { describe, expect, it } from 'vitest';

import { componentsSummary, revertibleApply } from './configured-project';

const run = (over: Partial<RunSummary>): RunSummary => ({
  runId: 'run-1',
  kind: 'apply',
  state: 'completed',
  startedAt: '2026-11-25T10:00:00Z',
  changesApplied: true,
  reverted: false,
  ...over
});

describe('revertibleApply', () => {
  it('a aplicação mais recente com alterações no projeto', () => {
    const runs = [
      run({ runId: 'velha', startedAt: '2026-11-24T10:00:00Z' }),
      run({ runId: 'nova' }),
      run({ runId: 'validacao', kind: 'validate', startedAt: '2026-11-25T11:00:00Z', changesApplied: false })
    ];
    expect(revertibleApply(runs)?.runId).toBe('nova');
  });

  it('pula a já revertida e a que não gravou nada', () => {
    const runs = [
      run({ runId: 'revertida', reverted: true }),
      run({ runId: 'velha', startedAt: '2026-11-24T10:00:00Z' })
    ];
    expect(revertibleApply(runs)?.runId).toBe('velha');
    expect(revertibleApply([run({ changesApplied: false })])).toBeNull();
  });

  it('não oferece reverter quando outra execução mais recente gravou no projeto: o motor recusaria', () => {
    const runs = [run({ runId: 'seguranca', kind: 'security', startedAt: '2026-11-25T12:00:00Z' }), run({})];
    expect(revertibleApply(runs)).toBeNull();
  });
});

describe('componentsSummary', () => {
  const status = (over: Partial<HarnessStatus>) =>
    ({ components: [{}, {}, {}], intact: 3, kept: 0, modified: 0, missing: 0, ...over }) as HarnessStatus;

  it('só as contagens que existem, com o plural certo', () => {
    expect(componentsSummary(status({}))).toBe('3 componentes: 3 íntegros');
    expect(componentsSummary(status({ intact: 1, modified: 1, missing: 1 }))).toBe(
      '3 componentes: 1 íntegro, 1 modificado à mão, 1 ausente'
    );
  });
});
