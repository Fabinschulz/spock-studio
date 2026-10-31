import type {
  Actor,
  ActorState,
  EngineIdentity,
  OperationDraft,
  OperationStep,
  OperationView
} from '@/prototype/types/operation';

const FINISHED_STATES = ['completed', 'completed_with_limitations', 'failed'];

function engineState(finished: boolean, running: OperationStep | null, hasEvents: boolean): ActorState {
  if (finished) return 'done';
  if (running) return 'working';
  return hasEvents ? 'waiting' : 'not_started';
}

function headlineOf(finished: boolean, running: OperationStep | null, steps: OperationStep[], hasEvents: boolean) {
  if (finished) return 'Execução concluída';
  if (running) {
    const done = steps.filter((s) => s.state === 'done' || s.state === 'warning').length;
    return `${running.title} · etapa ${Math.min(done + 1, steps.length)} de ${steps.length}`;
  }
  return hasEvents ? 'Aguardando a próxima etapa' : 'Pronto para executar';
}

export function deriveView(draft: OperationDraft, eventCount: number, identity: EngineIdentity): OperationView {
  const steps = draft.order.map((id) => draft.steps.get(id)).filter((s): s is OperationStep => s !== undefined);
  const running = steps.find((s) => s.state === 'running') ?? null;
  const finished = FINISHED_STATES.includes(draft.runState);
  const hasEvents = eventCount > 0;

  const engine: Actor = {
    ...identity,
    state: draft.runState === 'failed' ? 'failed' : engineState(finished, running, hasEvents),
    activity: running ? running.title : finished ? 'Execução concluída' : null,
    source: 'engine'
  };

  return {
    runState: draft.runState,
    steps,
    actors: [engine, ...draft.actors.values()],
    finished,
    headline: headlineOf(finished, running, steps, hasEvents)
  };
}
