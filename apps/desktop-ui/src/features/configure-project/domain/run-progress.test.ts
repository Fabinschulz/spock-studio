import { describe, expect, it } from 'vitest';

import applyResult from '@contracts/fixtures/golden/apply-result.json';
import plan from '@contracts/fixtures/golden/plan.json';

import { planSchema, type RunEvent } from '@/shared/schema';

import { initialJourneyState } from '../constants/initial-journey-state';
import { journeyReducer } from '../domain/journey-reducer';
import { type JourneyState } from '../types/journey';
import { plannedApplySteps } from './planned-steps';
import { runningStep, stepProgress } from './run-progress';

const parsedPlan = planSchema.parse(plan);
const event = (type: string, data: Record<string, unknown>): RunEvent => ({
  v: 1,
  kind: 'event',
  runId: 'r1',
  type,
  seq: 1,
  data
});

function started(approvedCommands: string[] = []): JourneyState {
  return journeyReducer(initialJourneyState, {
    type: 'run.start',
    kind: 'apply',
    at: 1000,
    planned: plannedApplySteps(parsedPlan, approvedCommands)
  });
}

describe('plannedApplySteps', () => {
  it('sem comando aprovado, só as etapas que o motor sempre executa, na ordem', () => {
    expect(plannedApplySteps(parsedPlan, []).map((s) => s.id)).toEqual([
      'precheck',
      'backup',
      'write',
      'lock',
      'validate'
    ]);
  });

  it('cada comando aprovado entra como etapa própria, com a descrição do comando, entre o backup e a escrita', () => {
    const command = parsedPlan.commands[0]!;
    const steps = plannedApplySteps(parsedPlan, [command.id]);

    expect(steps.map((s) => s.id)).toEqual([
      'precheck',
      'backup',
      `command:${command.id}`,
      'write',
      'lock',
      'validate'
    ]);
    expect(steps[2]?.title).toBe(command.description);
  });

  it('um comando não aprovado não entra', () => {
    expect(plannedApplySteps(parsedPlan, ['outro-id']).some((s) => s.id.startsWith('command:'))).toBe(false);
  });
});

describe('execução: progresso a partir dos eventos', () => {
  it('as etapas planejadas começam aguardando e nenhuma aparece como feita antes do motor dizer', () => {
    const run = started().run!;

    expect(run.steps.every((s) => s.status === 'pending')).toBe(true);
    expect(stepProgress(run)).toEqual({ done: 0, total: 5 });
    expect(runningStep(run)).toBeNull();
  });

  it('step.started marca a etapa em andamento no lugar dela e registra o instante', () => {
    let state = started();
    state = journeyReducer(state, {
      type: 'run.event',
      event: event('step.started', { id: 'backup', title: 'Guardando cópias' }),
      at: 2500
    });
    const run = state.run!;

    expect(run.steps.map((s) => s.id)).toEqual(['precheck', 'backup', 'write', 'lock', 'validate']);
    expect(run.steps[1]).toMatchObject({ status: 'running', title: 'Guardando cópias', startedAt: 2500 });
    expect(runningStep(run)?.id).toBe('backup');
  });

  it('step.completed conclui a etapa, guarda o detalhe e alimenta o feed em linguagem de pessoa', () => {
    let state = started();
    state = journeyReducer(state, {
      type: 'run.event',
      event: event('step.started', { id: 'precheck', title: 'Verificar pré-condições' }),
      at: 1500
    });
    state = journeyReducer(state, {
      type: 'run.event',
      event: event('step.completed', { id: 'precheck', status: 'completed', detail: null }),
      at: 2600
    });
    const run = state.run!;

    expect(run.steps[0]).toMatchObject({ status: 'completed', startedAt: 1500, endedAt: 2600 });
    expect(stepProgress(run).done).toBe(1);
    expect(run.feed.map((f) => f.text)).toEqual([
      'Iniciou: Verificar pré-condições',
      'Concluiu: Verificar pré-condições'
    ]);
  });

  it('uma etapa que o motor acrescenta e não estava prevista entra no fim, sem reordenar as demais', () => {
    let state = started();
    state = journeyReducer(state, {
      type: 'run.event',
      event: event('step.started', { id: 'extra', title: 'Etapa nova' }),
      at: 3000
    });

    expect(state.run!.steps.map((s) => s.id)).toEqual(['precheck', 'backup', 'write', 'lock', 'validate', 'extra']);
  });

  it('eventos de tipos desconhecidos são ignorados, não derrubam a tela', () => {
    const before = started().run!;
    const after = journeyReducer(started(), {
      type: 'run.event',
      event: event('agent.state', { agent: 'x' }),
      at: 1
    }).run!;

    expect(after.steps).toEqual(before.steps);
    expect(after.feed).toEqual([]);
  });

  it('a execução de validar ou reverter não declara etapas: só as que o motor publicar aparecem', () => {
    const state = journeyReducer(initialJourneyState, { type: 'run.start', kind: 'validate', at: 5 });

    expect(state.run!.steps).toEqual([]);
    expect(applyResult.kind).toBe('apply');
  });
});
