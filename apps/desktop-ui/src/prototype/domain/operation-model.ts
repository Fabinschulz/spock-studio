import type { ScriptedEvent } from '../data/demo-scripts';

/**
 * Modelo de visão de uma operação, derivado SOMENTE de eventos. Na aplicação real a entrada são os RunEvent do motor;
 * aqui são os eventos de um script de demonstração. Nada é inferido do que "deveria" acontecer, exceto a lista de
 * etapas esperadas (marcadas `expected`), que o motor deveria declarar (evento proposto `run.plan`).
 */

export type StepState = 'pending' | 'running' | 'done' | 'warning' | 'failed';
export type ActorState = 'not_started' | 'waiting' | 'working' | 'done' | 'failed';

export interface OperationStep {
  id: string;
  title: string;
  state: StepState;
  detail: string | null;
  startedAt: number | null;
  endedAt: number | null;
  /** Etapa conhecida de antemão (não vinda de um evento). */
  expected: boolean;
  files: string[];
  evals: EvalProgress[];
}

export interface EvalProgress {
  id: string;
  name: string;
  kind: string;
  state: 'running' | 'passed' | 'failed' | 'blocked' | 'not_run';
}

export interface Actor {
  id: string;
  name: string;
  state: ActorState;
  activity: string | null;
  source: 'engine' | 'proposed';
}

export interface OperationView {
  runState: string;
  steps: OperationStep[];
  actors: Actor[];
  finished: boolean;
  /** Frase que resume o que está acontecendo agora, montada só com o que os eventos informam. */
  headline: string;
}

export interface ExpectedStep {
  id: string;
  title: string;
}

const text = (data: Record<string, unknown>, key: string): string | null =>
  typeof data[key] === 'string' ? data[key] : null;

const stateFromStatus = (status: string | null): StepState =>
  status === 'completed' ? 'done' : status === 'failed' ? 'failed' : status ? 'warning' : 'done';

export function buildOperation(
  events: ScriptedEvent[],
  expected: readonly ExpectedStep[],
  engineActor: { id: string; name: string }
): OperationView {
  const steps = new Map<string, OperationStep>();
  const order: string[] = [];
  const actors = new Map<string, Actor>();
  let runState = 'pending';
  let currentStep: string | null = null;

  const ensure = (id: string, title: string, isExpected: boolean): OperationStep => {
    let step = steps.get(id);
    if (!step) {
      step = {
        id,
        title,
        state: 'pending',
        detail: null,
        startedAt: null,
        endedAt: null,
        expected: isExpected,
        files: [],
        evals: []
      };
      steps.set(id, step);
      order.push(id);
    }
    return step;
  };

  for (const e of expected) ensure(e.id, e.title, true);

  for (const e of events) {
    switch (e.type) {
      case 'run.plan': {
        const list = Array.isArray(e.data['steps']) ? (e.data['steps'] as ExpectedStep[]) : [];
        for (const s of list) ensure(s.id, s.title, true);
        break;
      }
      case 'run.state':
        runState = text(e.data, 'state') ?? runState;
        break;
      case 'step.started': {
        const id = text(e.data, 'id') ?? `step-${order.length}`;
        const step = ensure(id, text(e.data, 'title') ?? id, false);
        step.title = text(e.data, 'title') ?? step.title;
        step.state = 'running';
        step.startedAt = e.t;
        currentStep = id;
        break;
      }
      case 'step.completed': {
        const step = steps.get(text(e.data, 'id') ?? '');
        if (step) {
          step.state = stateFromStatus(text(e.data, 'status'));
          step.detail = text(e.data, 'detail');
          step.endedAt = e.t;
          if (currentStep === step.id) currentStep = null;
        }
        break;
      }
      case 'file.applied': {
        const step = currentStep ? steps.get(currentStep) : undefined;
        const path = text(e.data, 'path');
        if (step && path) step.files.push(path);
        break;
      }
      case 'eval.started': {
        const step = steps.get('validate');
        const id = text(e.data, 'id');
        if (step && id)
          step.evals.push({ id, name: text(e.data, 'name') ?? id, kind: text(e.data, 'kind') ?? '', state: 'running' });
        break;
      }
      case 'eval.completed': {
        const item = steps.get('validate')?.evals.find((x) => x.id === text(e.data, 'id'));
        const status = text(e.data, 'status');
        if (item && (status === 'passed' || status === 'failed' || status === 'blocked' || status === 'not_run'))
          item.state = status;
        break;
      }
      case 'agent.state': {
        const id = text(e.data, 'agent');
        if (!id) break;
        const raw = text(e.data, 'state');
        const state: ActorState =
          raw === 'working' || raw === 'done' || raw === 'failed' || raw === 'waiting' ? raw : 'not_started';
        actors.set(id, {
          id,
          name: text(e.data, 'name') ?? id,
          state,
          activity: text(e.data, 'activity'),
          source: 'proposed'
        });
        break;
      }
      case 'run.completed':
        runState = text(e.data, 'state') ?? 'completed';
        break;
    }
  }

  const list = order.map((id) => steps.get(id)!).filter(Boolean);
  const running = list.find((s) => s.state === 'running') ?? null;
  const finished = runState === 'completed' || runState === 'completed_with_limitations' || runState === 'failed';

  const engineState: ActorState = finished
    ? 'done'
    : running
      ? 'working'
      : events.length > 0
        ? 'waiting'
        : 'not_started';
  const engine: Actor = {
    id: engineActor.id,
    name: engineActor.name,
    state: runState === 'failed' ? 'failed' : engineState,
    activity: running ? running.title : finished ? 'Execução concluída' : null,
    source: 'engine'
  };

  const done = list.filter((s) => s.state === 'done' || s.state === 'warning').length;
  const headline = finished
    ? 'Execução concluída'
    : running
      ? `${running.title} · etapa ${Math.min(done + 1, list.length)} de ${list.length}`
      : events.length > 0
        ? 'Aguardando a próxima etapa'
        : 'Pronto para executar';

  return { runState, steps: list, actors: [engine, ...actors.values()], finished, headline };
}
