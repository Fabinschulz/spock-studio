import type { EvalResult } from '@/shared/schema';
import { ENGINE_NOTE, HARNESS_ENGINEER_NOTE, judgeStates } from '../constants/judge-states';
import type { Participant, ParticipantsContext } from '../types/participant';
import { HARNESS_ENGINEER_NOT_INVOKED } from './harness-engineer-prompt';
import { runningStep } from './run-progress';

const ENGINE = { id: 'engine', name: 'Motor Mariliz', note: ENGINE_NOTE } as const;

function judgeOf(evals: readonly EvalResult[]): Participant | null {
  const judge = evals.find((e) => e.kind === 'model');
  if (!judge) return null;
  return {
    id: judge.id,
    name: judge.name,
    ...judgeStates[judge.status],
    activity: judge.status === 'not_run' ? 'Não foi executado nesta validação.' : null
  };
}

function engineOf({ step, run, results }: ParticipantsContext): Participant {
  if (run && run.error === null) {
    return {
      ...ENGINE,
      glyph: 'running',
      state: 'Em atividade',
      activity: runningStep(run)?.title ?? 'Aguardando a próxima etapa'
    };
  }
  if (run) return { ...ENGINE, glyph: 'failed', state: 'Falhou' };

  const apply = results.apply;
  if (apply && step === 'result') {
    const succeeded = apply.outcome === 'completed' || apply.outcome === 'completed_with_limitations';
    return { ...ENGINE, glyph: succeeded ? 'done' : 'failed', state: succeeded ? 'Concluído' : 'Falhou' };
  }
  return { ...ENGINE, glyph: 'pending', state: 'Aguardando' };
}

function harnessEngineerOf({ plan }: ParticipantsContext): Participant | null {
  if (plan.status !== 'ready') return null;
  const waiting = plan.data.limitations.some((l) => l.code === HARNESS_ENGINEER_NOT_INVOKED);
  return {
    id: 'harness-engineer',
    name: 'Harness Engineer',
    glyph: waiting ? 'pending' : 'done',
    state: waiting ? 'Aguardando proposta' : 'Proposta recebida',
    activity: waiting ? 'Sem proposta: o plano usa as regras-base.' : null,
    note: HARNESS_ENGINEER_NOTE
  };
}

/** Só fatos: atividade de agente sem evento do motor não aparece. */
export function participantsOf(context: ParticipantsContext): Participant[] {
  const evals = (context.results.validate ?? context.results.apply)?.validation?.results ?? [];
  const judge = context.step === 'result' ? judgeOf(evals) : null;
  return [engineOf(context), harnessEngineerOf(context), judge].filter((p): p is Participant => p !== null);
}
