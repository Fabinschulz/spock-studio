import type { EvalResult } from '@/shared/schema';
import { ENGINE_NOTE, harnessEngineerNote, judgeStates } from '../constants/judge-states';
import type { Participant, ParticipantsContext } from '../types/participant';
import { HARNESS_ENGINEER_NOT_INVOKED } from './harness-engineer-prompt';
import { runningStep } from './run-progress';

const ENGINE = { id: 'engine', name: 'Motor Spock', note: ENGINE_NOTE } as const;

/** O juiz só aparece quando de fato julgou: "não executado" já está na lista de verificações da página. */
function judgeOf(evals: readonly EvalResult[]): Participant | null {
  const judge = evals.find((e) => e.kind === 'model');
  if (!judge || judge.status === 'not_run') return null;
  return {
    id: judge.id,
    name: judge.name,
    ...judgeStates[judge.status],
    activity: null
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

/** O agente só aparece quando entregou a proposta; a falta dela a página já diz no aviso do plano e no Resultado. */
function harnessEngineerOf({ plan, naming }: ParticipantsContext): Participant | null {
  if (plan.status !== 'ready') return null;
  if (plan.data.limitations.some((l) => l.code === HARNESS_ENGINEER_NOT_INVOKED)) return null;
  return {
    id: 'harness-engineer',
    name: 'Harness Engineer',
    glyph: 'done',
    state: 'Proposta recebida',
    activity: null,
    note: harnessEngineerNote(naming.proposal)
  };
}

/** Só quem agiu ou está agindo: quem espera ou não rodou já aparece na própria página. */
export function participantsOf(context: ParticipantsContext): Participant[] {
  const evals = (context.results.validate ?? context.results.apply)?.validation?.results ?? [];
  const judge = context.step === 'result' ? judgeOf(evals) : null;
  return [engineOf(context), harnessEngineerOf(context), judge].filter((p): p is Participant => p !== null);
}
