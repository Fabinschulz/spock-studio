import applyResult from '@contracts/fixtures/golden/apply-result.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import { planSchema, runResultSchema } from '@/shared/schema';
import { describe, expect, it } from 'vitest';

import { participantsOf } from './participants';
import { buildSummaryFacts } from './summary-facts';
import { HARNESS_ENGINEER_NOT_INVOKED } from './harness-engineer-prompt';

const plan = planSchema.parse(planFixture);
const naming = { proposal: '.spock/proposal.json' } as Parameters<typeof participantsOf>[0]['naming'];
const noResults = { apply: null, revert: null, validate: null };

describe('Resumo do painel', () => {
  it('conta o plano como a Revisão: arquivos que mudam e comandos', () => {
    const files = plan.changes.filter((change) => change.action !== 'preserve').length;
    const facts = Object.fromEntries(
      buildSummaryFacts({
        step: 'review',
        projectName: 'Loja',
        aiEnvironments: ['claude-code'],
        plan: { status: 'ready', data: plan },
        approvalCount: 3
      })
    );
    expect(facts.Plano).toBe(
      `${files} arquivos · ${plan.commands.length === 1 ? '1 comando' : `${plan.commands.length} comandos`}`
    );
    expect(facts.Plano).not.toMatch(/alteraç/);
  });
});

describe('Participantes', () => {
  const context = (over: Partial<Parameters<typeof participantsOf>[0]>) =>
    participantsOf({ step: 'plan', run: null, results: noResults, plan: { status: 'idle' }, naming, ...over });

  it('o Harness Engineer só aparece quando entregou a proposta', () => {
    const waiting = {
      ...plan,
      limitations: [{ code: HARNESS_ENGINEER_NOT_INVOKED, message: 'sem proposta' }]
    };
    expect(context({ plan: { status: 'ready', data: waiting } }).map((p) => p.id)).toEqual(['engine']);
    const received = { ...plan, limitations: [] };
    expect(context({ plan: { status: 'ready', data: received } }).map((p) => p.id)).toEqual([
      'engine',
      'harness-engineer'
    ]);
  });

  it('o juiz por modelo só aparece quando julgou', () => {
    const apply = runResultSchema.parse(applyResult);
    const withJudge = (status: 'not_run' | 'passed') => ({
      ...apply,
      validation: {
        outcome: 'passed' as const,
        results: [
          {
            id: 'EV-JUDGE',
            name: 'Juiz por modelo',
            property: '',
            kind: 'model' as const,
            required: false,
            status,
            criteria: '',
            evidence: [],
            reason: null,
            limitations: []
          }
        ]
      }
    });
    const ids = (status: 'not_run' | 'passed') =>
      context({ step: 'result', results: { ...noResults, apply: withJudge(status) } }).map((p) => p.id);
    expect(ids('not_run')).toEqual(['engine']);
    expect(ids('passed')).toEqual(['engine', 'EV-JUDGE']);
  });
});
