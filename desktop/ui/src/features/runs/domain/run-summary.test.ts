import runDetailFixture from '@contracts/fixtures/runs/run-detail.json';

import type { RunDetail } from '@/shared/schema';
import { describe, expect, it } from 'vitest';

import { deliverySummary } from './run-summary';

const base = runDetailFixture as unknown as RunDetail;

const detail = (summary: Partial<RunDetail['summary']>, kind = 'apply'): RunDetail => ({
  ...base,
  kind,
  summary: { ...base.summary, ...summary }
});

const done = (overrides: Partial<RunDetail['summary']['done']> = {}): RunDetail['summary']['done'] => ({
  created: 0,
  updated: 0,
  removed: 0,
  reverted: 0,
  stepsCompleted: 0,
  stepsTotal: 0,
  actionsExecuted: 0,
  actionsTotal: 0,
  ...overrides
});

const leftOut = (overrides: Partial<RunDetail['summary']['leftOut']> = {}): RunDetail['summary']['leftOut'] => ({
  notApplied: 0,
  pending: 0,
  conflicts: 0,
  actionsNotExecuted: 0,
  ...overrides
});

describe('resumo de entrega', () => {
  it('com a fixture do motor: um arquivo criado, um passo concluído e uma verificação que passou', () => {
    const summary = deliverySummary(base);

    expect(summary.request).toBe('Aplicar a configuração do Harness no projeto.');
    expect(summary.done).toEqual(['Criou 1 arquivo.', '1 passo concluído de 1.']);
    expect(summary.validated).toBe('1 de 1 verificação passou.');
    expect(summary.leftOut).toEqual([]);
  });

  it('o pedido diz o que o tipo de execução faz, e um tipo novo não quebra', () => {
    expect(deliverySummary(detail({}, 'revert')).request).toBe('Reverter uma aplicação anterior do Harness.');
    expect(deliverySummary(detail({}, 'validate')).request).toBe(
      'Validar o projeto contra as verificações do Harness.'
    );
    expect(deliverySummary(detail({}, 'assistant')).request).toBe('Pergunta ao assistente gerencial sobre o projeto.');
    expect(deliverySummary(detail({}, 'algo-novo')).request).toBe('Execução do Spock Studio.');
  });

  it('soma criados, atualizados, removidos e revertidos numa frase só', () => {
    const summary = deliverySummary(detail({ done: done({ created: 2, updated: 1, removed: 3, reverted: 1 }) }));

    expect(summary.done[0]).toBe('Criou 2 arquivos, atualizou 1 arquivo, removeu 3 arquivos, reverteu 1 arquivo.');
  });

  it('sem arquivo nenhum, diz que nada foi alterado em vez de ficar em branco', () => {
    expect(deliverySummary(detail({ done: done() })).done).toEqual(['Nenhum arquivo foi alterado.']);
  });

  it('conta as ações propostas por agentes', () => {
    const summary = deliverySummary(detail({ done: done({ actionsExecuted: 1, actionsTotal: 3 }) }));

    expect(summary.done).toContain('1 ação proposta por agentes executada de 3.');
  });

  it('sem verificação registrada, diz isso e não mostra zero', () => {
    const summary = deliverySummary(detail({ validated: null }));

    expect(summary.validated).toBe('Nenhuma verificação foi registrada nesta execução.');
  });

  it('diz o que falhou, o que ficou bloqueado, o que não rodou e as obrigatórias que não passaram', () => {
    const summary = deliverySummary(
      detail({ validated: { total: 6, passed: 2, failed: 1, blocked: 2, notRun: 1, requiredNotPassed: 2 } })
    );

    expect(summary.validated).toBe(
      '2 de 6 verificações passaram; 1 falhou, 2 ficaram bloqueadas, 1 não rodou. 2 obrigatórias não passaram.'
    );
  });

  it('lista o que ficou de fora, uma frase por assunto', () => {
    const summary = deliverySummary(
      detail({ leftOut: leftOut({ notApplied: 2, conflicts: 1, actionsNotExecuted: 1, pending: 3 }) })
    );

    expect(summary.leftOut).toEqual([
      '2 alterações não foram aplicadas.',
      '1 arquivo em conflito.',
      '1 ação proposta não foi executada.',
      '3 itens pendentes.'
    ]);
  });
});
