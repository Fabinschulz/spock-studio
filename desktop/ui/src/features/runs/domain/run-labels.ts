import type { Tone } from '@/shared/components';
import { RUN_KINDS, RUN_STATES, type RunItem } from '@/shared/schema';
import { runStateLabel } from '@/shared/utils';

const KIND_LABEL: Record<string, string> = {
  apply: 'Configuração aplicada',
  validate: 'Validação',
  revert: 'Reversão',
  workflow: 'Workflow'
};

/** Um tipo novo do motor aparece como veio, nunca some. */
export function runKindLabel(kind: string): string {
  return KIND_LABEL[kind] ?? kind;
}

export const KIND_OPTIONS = RUN_KINDS.map((kind) => ({ value: kind, label: runKindLabel(kind) }));
export const STATE_OPTIONS = RUN_STATES.map((state) => ({ value: state, label: runStateLabel(state) }));

/**
 * "Andando" no diário não basta: depois de uma queda o diário continua dizendo isso e ninguém executa. O motor diz
 * em `live` se algo está rodando agora; sem isso, a execução foi interrompida.
 */
export function isInterrupted(run: Pick<RunItem, 'state' | 'live'>): boolean {
  return (run.state === 'running' || run.state === 'validating') && !run.live;
}

export function displayState(run: Pick<RunItem, 'state' | 'live'>): string {
  return isInterrupted(run) ? 'Interrompida' : runStateLabel(run.state);
}

export function stateTone(run: Pick<RunItem, 'state' | 'live'>): Tone {
  if (isInterrupted(run)) return 'warning';
  switch (run.state) {
    case 'completed':
      return 'success';
    case 'completed_with_limitations':
    case 'requires_intervention':
      return 'warning';
    case 'failed':
      return 'danger';
    case 'running':
    case 'validating':
    case 'awaiting_approval':
      return 'info';
    default:
      return 'neutral';
  }
}

/** Frase curta do que a execução deixou no projeto; vazia quando não há o que dizer. */
export function effectLabel(run: Pick<RunItem, 'kind' | 'changesApplied' | 'reverted'>): string {
  if (run.reverted) return 'revertida depois';
  if (run.kind === 'apply' && run.changesApplied) return 'alterações aplicadas';
  return '';
}

/**
 * O próximo passo de uma execução que não terminou bem, dito em uma frase. É orientação, não ação: nada aqui dispara
 * nada. Vazio quando a execução terminou bem ou ainda está andando.
 */
export function nextStep(run: RunItem): string {
  if (isInterrupted(run))
    return 'O aplicativo foi fechado no meio desta execução. Confira o que ela mudou abaixo e execute de novo se precisar.';
  switch (run.state) {
    case 'failed':
      return run.canRevert
        ? 'A execução falhou depois de aplicar parte das alterações. Revise o que mudou abaixo e, se preciso, reverta pela jornada Configurar.'
        : 'A execução falhou e nada foi aplicado. Corrija o motivo abaixo e execute de novo.';
    case 'requires_intervention':
      return 'Esta execução precisa de uma decisão sua antes de seguir. Confira o que ficou pendente abaixo.';
    case 'completed_with_limitations':
      return 'Terminou, mas com limitações. Veja abaixo o que ficou de fora.';
    case 'awaiting_approval':
      return 'Esta execução espera a sua aprovação. Abra o acompanhamento para decidir.';
    default:
      return '';
  }
}

/** Só quem anda agora ou espera aprovação tem acompanhamento ao vivo; as demais já terminaram e ficam no detalhe. */
export function hasLiveView(run: Pick<RunItem, 'live' | 'state'>): boolean {
  return run.live || run.state === 'awaiting_approval';
}
