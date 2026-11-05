import { describe, expect, it } from 'vitest';

import applyFailure from '@contracts/fixtures/golden/apply-failure.json';
import applyResult from '@contracts/fixtures/golden/apply-result.json';
import discoveryFixture from '@contracts/fixtures/golden/discovery.json';
import planFixture from '@contracts/fixtures/golden/plan.json';
import { discoverySchema, planSchema, runResultSchema, type RunEvent } from '@/shared/schema';

import { initialJourneyState } from '../constants/initial-journey-state';
import type { JourneyState } from '../types/journey';
import { buildApplyInput, countApprovals, defaultApprovals } from './approvals';
import { journeyReducer } from './journey-reducer';
import { applyRunEvent } from './run-events';
import { awaitingHarnessProposal, explainOutcome, outcomeSeverity } from './run-outcome';

/**
 * Caracterização da jornada "Configurar projeto" com as fixtures geradas pelo motor real. Fixa o comportamento atual
 * antes das refatorações da Onda 2: se um destes testes mudar, o comportamento mudou.
 */
const discovery = discoverySchema.parse(discoveryFixture);
const plan = planSchema.parse(planFixture);
const conflictId = plan.changes.find((change) => change.action === 'conflict')!.id;
const createIds = plan.changes.filter((change) => change.action === 'create').map((change) => change.id);
const preserveId = plan.changes.find((change) => change.action === 'preserve')!.id;

const event = (type: string, data: Record<string, unknown>): RunEvent => ({
  v: 1,
  kind: 'event',
  runId: 'run-1',
  seq: 0,
  type,
  data
});

function planned(): JourneyState {
  return journeyReducer(journeyReducer(initialJourneyState, { type: 'discover.ok', discovery }), {
    type: 'plan.ok',
    plan
  });
}

describe('reducer da jornada', () => {
  it('sugere o ambiente de IA quando a descoberta só encontra um, e não sobrescreve a escolha da pessoa', () => {
    expect(journeyReducer(initialJourneyState, { type: 'discover.ok', discovery }).aiEnvironment).toBe('claude-code');
    const chosen = journeyReducer(initialJourneyState, { type: 'environment.set', id: 'cursor' });
    expect(journeyReducer(chosen, { type: 'discover.ok', discovery }).aiEnvironment).toBe('cursor');
  });

  it('plano novo vai para a etapa do plano com criar/atualizar marcados, comandos desmarcados e conflitos mantidos', () => {
    const state = planned();
    expect(state.step).toBe('plan');
    expect(state.approvedChanges).toEqual(createIds);
    expect(state.approvedCommands).toEqual([]);
    expect(state.resolutions).toEqual({ [conflictId]: 'keep' });
  });

  it('alterna aprovações e resoluções', () => {
    let state = journeyReducer(planned(), { type: 'change.toggle', id: createIds[0]! });
    expect(state.approvedChanges).not.toContain(createIds[0]);
    state = journeyReducer(state, { type: 'change.toggle', id: createIds[0]! });
    expect(state.approvedChanges).toContain(createIds[0]);
    state = journeyReducer(state, { type: 'command.toggle', id: 'specification-install' });
    expect(state.approvedCommands).toEqual(['specification-install']);
    state = journeyReducer(state, { type: 'resolution.set', id: conflictId, value: 'overwrite' });
    expect(state.resolutions[conflictId]).toBe('overwrite');
  });

  it('aplicação abre a etapa de execução com as etapas planejadas pendentes; validação e reversão não mudam de etapa', () => {
    const base = planned();
    const apply = journeyReducer(base, {
      type: 'run.start',
      kind: 'apply',
      at: 5,
      planned: [{ id: 'backup', title: 'Backup' }]
    });
    expect(apply.step).toBe('run');
    expect(apply.run).toMatchObject({ kind: 'apply', runId: null, state: 'running', startedAt: 5 });
    expect(apply.run!.steps).toEqual([
      { id: 'backup', title: 'Backup', status: 'pending', detail: null, startedAt: null, endedAt: null }
    ]);
    expect(journeyReducer(base, { type: 'run.start', kind: 'validate' }).step).toBe('plan');
  });

  it('ids, eventos e fim de execução só valem com execução em andamento', () => {
    const idle = planned();
    expect(journeyReducer(idle, { type: 'run.id', runId: 'run-1' })).toBe(idle);
    expect(journeyReducer(idle, { type: 'run.event', event: event('run.state', { state: 'x' }) })).toBe(idle);
    expect(journeyReducer(idle, { type: 'run.error', message: 'x' })).toBe(idle);

    const running = journeyReducer(journeyReducer(idle, { type: 'run.start', kind: 'apply' }), {
      type: 'run.id',
      runId: 'run-1'
    });
    expect(running.run!.runId).toBe('run-1');
    const failed = journeyReducer(running, { type: 'run.error', message: 'caiu' });
    expect(failed.run).toMatchObject({ state: 'failed', error: 'caiu' });

    const result = runResultSchema.parse(applyResult);
    const done = journeyReducer(running, { type: 'run.done', result });
    expect(done.step).toBe('result');
    expect(done.run).toBeNull();
    expect(done.results.apply).toBe(result);
  });
});

describe('aprovações', () => {
  it('substituir um conflito conta como aprovar a mudança; ids fora do plano não vão para o motor', () => {
    const state: JourneyState = {
      ...planned(),
      approvedChanges: [createIds[0]!, 'chg-de-outro-plano'],
      resolutions: { [conflictId]: 'overwrite' }
    };
    const input = buildApplyInput(state, 'p-1', plan);
    expect(input).toEqual({
      projectId: 'p-1',
      planId: plan.planId,
      approvedChangeIds: [createIds[0], conflictId],
      approvedCommandIds: [],
      resolutions: { [conflictId]: 'overwrite' }
    });
    expect(countApprovals(state, plan)).toBe(2);
  });

  it('preservar nunca entra nas aprovações padrão', () => {
    expect(defaultApprovals(plan).approvedChanges).not.toContain(preserveId);
  });
});

describe('eventos da execução', () => {
  const run = journeyReducer(planned(), {
    type: 'run.start',
    kind: 'apply',
    planned: [{ id: 'backup', title: 'Backup' }]
  }).run!;

  it('etapa conhecida é atualizada no lugar; desconhecida é acrescentada; o feed conta o que aconteceu', () => {
    const started = applyRunEvent(run, event('step.started', { id: 'backup', title: 'Backup' }), 10);
    expect(started.steps).toEqual([
      { id: 'backup', title: 'Backup', status: 'running', detail: null, startedAt: 10, endedAt: null }
    ]);
    const extra = applyRunEvent(started, event('step.started', { id: 'lock', title: 'Lock' }), 11);
    expect(extra.steps.map((step) => step.id)).toEqual(['backup', 'lock']);
    const completed = applyRunEvent(
      extra,
      event('step.completed', { id: 'backup', status: 'completed', detail: '3 arquivos' }),
      12
    );
    expect(completed.steps[0]).toMatchObject({ status: 'completed', detail: '3 arquivos', endedAt: 12 });
    expect(completed.feed.map((item) => item.text)).toEqual(['Iniciou: Backup', 'Iniciou: Lock', 'Concluiu: Backup']);
  });

  it('run.plan troca a lista local pela do motor, na ordem dele, sem perder o que já começou', () => {
    const started = applyRunEvent(run, event('step.started', { id: 'backup', title: 'Backup' }), 10);
    const announced = applyRunEvent(
      started,
      event('run.plan', {
        steps: [
          { id: 'precheck', title: 'Verificar' },
          { id: 'backup', title: 'Backup' },
          { id: 'command:x', title: 'Instalar' },
          { title: 'sem id' }
        ]
      }),
      11
    );

    expect(announced.steps.map((step) => [step.id, step.status])).toEqual([
      ['precheck', 'pending'],
      ['backup', 'running'],
      ['command:x', 'pending']
    ]);
    expect(announced.steps[1]?.startedAt).toBe(10);
    // Uma lista que não diz nada não apaga o que a interface já sabia.
    expect(applyRunEvent(announced, event('run.plan', { steps: 'nada' }), 12).steps).toBe(announced.steps);
    expect(applyRunEvent(announced, event('run.plan', { steps: [] }), 12).steps).toBe(announced.steps);
  });

  it('eval.started e eval.completed acompanham cada verificação, na ordem em que chegam', () => {
    const one = applyRunEvent(run, event('eval.started', { id: 'EV-1', name: 'Agentes', kind: 'deterministic' }), 1);
    const two = applyRunEvent(one, event('eval.started', { id: 'EV-J', name: 'Juiz', kind: 'model' }), 2);
    expect(two.evals.map((e) => [e.id, e.status])).toEqual([
      ['EV-1', 'running'],
      ['EV-J', 'running']
    ]);

    const done = applyRunEvent(two, event('eval.completed', { id: 'EV-1', status: 'passed' }), 3);
    expect(done.evals[0]).toMatchObject({ name: 'Agentes', kind: 'deterministic', status: 'passed' });
    expect(done.evals[1]).toMatchObject({ kind: 'model', status: 'running' });
    // Resultado de um Eval que nunca foi anunciado, ou sem status, não inventa uma linha.
    expect(applyRunEvent(done, event('eval.completed', { id: 'EV-X', status: 'passed' }), 4).evals).toHaveLength(2);
    expect(applyRunEvent(done, event('eval.completed', { id: 'EV-J' }), 4)).toBe(done);
  });

  it('usa o instante que o motor carimbou e cai no da chegada quando o evento não o traz', () => {
    const stamped = { ...event('step.started', { id: 'backup', title: 'Backup' }), at: '2026-11-05T12:30:00.500Z' };
    const withStamp = applyRunEvent(run, stamped, 999);
    expect(withStamp.steps[0]?.startedAt).toBe(Date.parse('2026-11-05T12:30:00.500Z'));
    expect(withStamp.feed[0]?.at).toBe(Date.parse('2026-11-05T12:30:00.500Z'));

    const invalid = { ...stamped, at: 'ontem' };
    expect(applyRunEvent(run, invalid, 999).steps[0]?.startedAt).toBe(999);
    expect(applyRunEvent(run, event('step.started', { id: 'backup', title: 'Backup' }), 999).steps[0]?.startedAt).toBe(
      999
    );
  });

  it('arquivo aplicado entra na lista; estado acompanha run.state; evento desconhecido não muda nada', () => {
    const file = applyRunEvent(run, event('file.applied', { op: 'create', path: '.claude/agents/x.md' }), 1);
    expect(file.files).toEqual(['create .claude/agents/x.md']);
    expect(applyRunEvent(run, event('run.state', { state: 'validating' }), 1).state).toBe('validating');
    expect(applyRunEvent(run, event('outro', {}), 1)).toBe(run);
  });
});

describe('resultado', () => {
  it('cada desfecho tem gravidade e explicação próprias, nunca um sucesso genérico', () => {
    const ok = runResultSchema.parse(applyResult);
    const failure = runResultSchema.parse(applyFailure);
    expect(outcomeSeverity[ok.outcome]).toBe('success');
    expect(explainOutcome(ok)).toBe('Todas as verificações obrigatórias foram aprovadas com evidência.');
    expect(outcomeSeverity[failure.outcome]).toBe('error');
    expect(explainOutcome(failure)).toMatch(/falhou/);
  });

  it('pede a proposta do Harness Engineer só depois de aplicado e enquanto não foi revertido', () => {
    const applied = runResultSchema.parse(applyResult);
    expect(awaitingHarnessProposal(applied, null)).toBe(true);
    expect(awaitingHarnessProposal(null, null)).toBe(false);
    expect(awaitingHarnessProposal({ ...applied, rolledBack: true }, null)).toBe(false);
    expect(awaitingHarnessProposal(applied, { ...applied, kind: 'revert', outcome: 'completed' })).toBe(false);
  });
});
