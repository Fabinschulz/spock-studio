import { describe, expect, it } from 'vitest';

import {
  applyScriptCurrent,
  applyScriptProposed,
  EXPECTED_APPLY_STEPS,
  planningScriptProposed
} from '../data/demo-scripts';

import { buildOperation } from './operation-model';

const engine = { id: 'engine', name: 'Motor Mariliz' };
const upTo = (script: typeof applyScriptCurrent, ms: number) => script.filter((e) => e.t <= ms);

describe('buildOperation', () => {
  it('antes de qualquer evento mostra as etapas esperadas como aguardando e nenhum participante ativo', () => {
    const view = buildOperation([], EXPECTED_APPLY_STEPS, engine);

    expect(view.steps.map((s) => s.state)).toEqual(['pending', 'pending', 'pending', 'pending', 'pending']);
    expect(view.headline).toBe('Pronto para executar');
    expect(view.actors).toEqual([expect.objectContaining({ id: 'engine', state: 'not_started', activity: null })]);
  });

  it('marca como em andamento só a etapa que o motor iniciou, e diz qual em linguagem de pessoa', () => {
    const view = buildOperation(upTo(applyScriptCurrent, 2000), EXPECTED_APPLY_STEPS, engine);

    expect(view.steps.find((s) => s.id === 'precheck')?.state).toBe('done');
    expect(view.steps.find((s) => s.id === 'backup')?.state).toBe('running');
    expect(view.steps.find((s) => s.id === 'write')?.state).toBe('pending');
    expect(view.headline).toBe('Guardar cópia do que será alterado · etapa 2 de 5');
    expect(view.actors[0]).toMatchObject({ state: 'working', activity: 'Guardar cópia do que será alterado' });
  });

  it('atribui cada arquivo gravado à etapa que estava em andamento', () => {
    const view = buildOperation(upTo(applyScriptCurrent, 4000), EXPECTED_APPLY_STEPS, engine);
    const write = view.steps.find((s) => s.id === 'write');

    expect(write?.state).toBe('running');
    expect(write?.files.length).toBeGreaterThan(0);
    expect(view.steps.filter((s) => s.id !== 'write').every((s) => s.files.length === 0)).toBe(true);
  });

  it('no contrato atual a validação é uma etapa só: nenhum Eval individual aparece', () => {
    const view = buildOperation(upTo(applyScriptCurrent, 11000), EXPECTED_APPLY_STEPS, engine);

    expect(view.steps.find((s) => s.id === 'validate')?.state).toBe('running');
    expect(view.steps.every((s) => s.evals.length === 0)).toBe(true);
  });

  it('conclui quando o motor diz que concluiu', () => {
    const view = buildOperation(applyScriptCurrent, EXPECTED_APPLY_STEPS, engine);

    expect(view.finished).toBe(true);
    expect(view.headline).toBe('Execução concluída');
    expect(view.steps.every((s) => s.state === 'done')).toBe(true);
  });

  it('com o contrato proposto, as etapas vêm do evento run.plan e cada Eval aparece com seu estado', () => {
    const view = buildOperation(upTo(applyScriptProposed, 11000), [], engine);
    const validate = view.steps.find((s) => s.id === 'validate');

    expect(view.steps.map((s) => s.id)).toEqual(['precheck', 'backup', 'write', 'lock', 'validate']);
    expect(validate?.evals.length).toBeGreaterThan(0);
    expect(validate?.evals.some((e) => e.state === 'passed')).toBe(true);
  });

  it('agentes só existem se houver evento deles; sem evento, só o motor aparece', () => {
    expect(buildOperation(upTo(applyScriptCurrent, 9000), EXPECTED_APPLY_STEPS, engine).actors).toHaveLength(1);

    const planning = buildOperation(upTo(planningScriptProposed, 2000), [], engine);
    expect(planning.actors.map((a) => a.id)).toEqual(['engine', 'harness-engineer']);
    expect(planning.actors[1]).toMatchObject({ state: 'working', source: 'proposed' });
  });
});
