import { describe, expect, it } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import applyResult from '@contracts/fixtures/golden/apply-result.json';
import planFixture from '@contracts/fixtures/golden/plan.json';

import { planSchema, runResultSchema } from '@/shared/schema';

import { initialJourneyState } from '../constants/initial-journey-state';
import { stepIssue } from '../domain/step-issue';
import { type JourneyState } from '../types/journey';
import { type RunView } from '../types/run';

const plan = planSchema.parse(planFixture);
const at = (patch: Partial<JourneyState>): JourneyState => ({ ...initialJourneyState, ...patch });

describe('stepIssue · problema da etapa para o indicador de progresso', () => {
  it('sem problema na jornada normal', () => {
    expect(stepIssue(at({ step: 'project', discovery: { status: 'ready', data: {} as never } }))).toBeUndefined();
    expect(stepIssue(at({ step: 'review', plan: { status: 'ready', data: plan } }))).toBeUndefined();
  });

  it('falha na análise marca a etapa Projeto com erro', () => {
    expect(stepIssue(at({ step: 'project', discovery: { status: 'error', message: 'x' } }))).toEqual({
      stepId: 'project',
      kind: 'error'
    });
  });

  it('falha ao gerar o plano marca Modelo e ambiente com erro', () => {
    expect(stepIssue(at({ step: 'model', plan: { status: 'error', message: 'x' } }))).toEqual({
      stepId: 'model',
      kind: 'error'
    });
  });

  it('um componente bloqueado pelo motor marca o Plano como bloqueado, e só então', () => {
    const blocked = {
      ...plan,
      decisions: [{ ...plan.decisions[0]!, decision: 'blocked' }, ...plan.decisions.slice(1)]
    };

    expect(stepIssue(at({ step: 'plan', plan: { status: 'ready', data: blocked } }))).toEqual({
      stepId: 'plan',
      kind: 'blocked'
    });
    expect(stepIssue(at({ step: 'plan', plan: { status: 'ready', data: plan } }))).toBeUndefined();
  });

  it('erro no acompanhamento da aplicação marca a etapa Aplicação', () => {
    const run: RunView = {
      kind: 'apply',
      runId: 'r',
      state: 'failed',
      steps: [],
      files: [],
      feed: [],
      startedAt: 0,
      error: 'x'
    };

    expect(stepIssue(at({ step: 'run', run }))).toEqual({ stepId: 'run', kind: 'error' });
    expect(stepIssue(at({ step: 'run', run: { ...run, error: null } }))).toBeUndefined();
  });

  it('o resultado só é erro quando a aplicação falhou; concluído com limitações não é erro', () => {
    const failed = runResultSchema.parse(applyFailure);
    const limited = { ...runResultSchema.parse(applyResult), outcome: 'completed_with_limitations' as const };
    const completed = runResultSchema.parse(applyResult);
    const state = (apply: typeof failed) => at({ step: 'result', results: { apply, revert: null, validate: null } });

    expect(stepIssue(state(failed))).toEqual({ stepId: 'result', kind: 'error' });
    expect(stepIssue(state(limited))).toBeUndefined();
    expect(stepIssue(state(completed))).toBeUndefined();
  });
});
