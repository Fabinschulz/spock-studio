import type { Tone } from '@/shared/components';
import type { BootRecord, BootResult, bootListSchema } from '@/shared/schema';
import type { z } from 'zod';

export type BootListItem = z.infer<typeof bootListSchema>['boots'][number];

export const OUTCOME_LABEL: Record<BootResult['outcome'], string> = {
  answered: 'Consultado',
  prepared: 'Preparado',
  handoff: 'Pronto para continuar',
  needsClarification: 'Preciso de uma escolha',
  needsInput: 'Falta algo',
  unsupported: 'Fora do que o Spock faz agora',
  failed: 'Não deu certo'
};

const STATE_LABEL: Record<BootRecord['state'], string> = {
  created: 'Começando',
  interpreting: 'Entendendo o pedido',
  awaitingClarification: 'Esperando sua escolha',
  preparing: 'Preparando',
  planned: 'Planejado',
  executing: 'Em andamento',
  completed: 'Concluído',
  failed: 'Não deu certo',
  cancelled: 'Cancelado',
  interrupted: 'Interrompido'
};

const OUTCOME_TONE: Record<BootResult['outcome'], Tone> = {
  answered: 'success',
  prepared: 'success',
  handoff: 'success',
  needsClarification: 'info',
  needsInput: 'warning',
  unsupported: 'warning',
  failed: 'danger'
};

/** Um Boot que terminou fala pelo resultado; os outros, pelo estado em que o motor os deixou. */
export function listedLabel(item: Pick<BootListItem, 'state' | 'outcome'>): string {
  const finished = item.state === 'completed' || item.state === 'awaitingClarification';
  return finished && item.outcome ? OUTCOME_LABEL[item.outcome] : STATE_LABEL[item.state];
}

export function listedTone(item: Pick<BootListItem, 'state' | 'outcome'>): Tone {
  switch (item.state) {
    case 'completed':
    case 'awaitingClarification':
      return item.outcome ? OUTCOME_TONE[item.outcome] : 'neutral';
    case 'interrupted':
      return 'warning';
    case 'failed':
      return 'danger';
    case 'cancelled':
      return 'neutral';
    default:
      return 'info';
  }
}

/** Só o que a queda deixou no meio e ainda não foi recuperado: o motor recusa recuperar duas vezes. */
export function canResume(item: Pick<BootListItem, 'state' | 'recoveredByBootId'>): boolean {
  return item.state === 'interrupted' && item.recoveredByBootId === null;
}
