import {
  ENGINE_IDENTITY,
  HARNESS_ENGINEER_NOTE,
  NO_PROPOSAL_ACTIVITY,
  NO_PROPOSAL_CODE
} from '@/prototype/constants/harness';
import type { Actor } from '@/prototype/types/operation';
import type { Participant, ParticipantsInput } from '@/prototype/types/participant';

const toParticipant = (actor: Actor): Participant => ({ ...actor, demo: actor.source === 'proposed' });

function executionParticipants({ slug, operation, judge }: ParticipantsInput): Participant[] {
  if (slug === 'aplicacao') return operation.actors.map(toParticipant);

  const [engine] = operation.actors;
  const participants = engine ? [toParticipant({ ...engine, state: 'done', activity: 'Execução concluída' })] : [];
  if (judge) {
    participants.push({
      id: 'judge',
      name: judge.name,
      state: judge.status === 'passed' ? 'done' : 'not_started',
      activity: judge.status === 'not_run' ? 'Não executado nesta validação' : null
    });
  }
  return participants;
}

function idleParticipants({ stepIndex, plan }: ParticipantsInput): Participant[] {
  const engine: Participant = { ...ENGINE_IDENTITY, state: 'not_started', activity: null, note: 'Executa localmente.' };
  if (stepIndex < 2) return [engine];

  const proposalMissing = plan.limitations.some((l) => l.code === NO_PROPOSAL_CODE);
  const harnessEngineer: Participant = {
    id: 'harness-engineer',
    name: 'Harness Engineer',
    state: 'waiting',
    activity: proposalMissing ? NO_PROPOSAL_ACTIVITY : 'Proposta recebida.',
    note: HARNESS_ENGINEER_NOTE
  };
  return [engine, harnessEngineer];
}

export function deriveParticipants(input: ParticipantsInput): Participant[] {
  if (input.slug === 'aplicacao' || input.slug === 'resultado') return executionParticipants(input);
  if (input.slug === 'plano' && input.contract === 'proposed') {
    return input.planning.actors.filter((a) => a.source === 'proposed').map(toParticipant);
  }
  return idleParticipants(input);
}
