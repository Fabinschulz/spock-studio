import type { Tone } from '@/shared/components/ui';

/** O que aconteceu com cada ação proposta, em frase para quem lê o resultado. */
export const ACTION_STATUS_LABEL: Record<string, string> = {
  executed: 'executada',
  skipped: 'não aprovada, nada foi gravado',
  conflict: 'não gravada: já existe um arquivo diferente neste caminho',
  failed: 'não foi possível executar',
  approved: 'aprovada, aguardando a execução',
  proposed: 'proposta'
};

export const AREA_LABEL: Record<string, string> = {
  servico: 'Serviço',
  interface: 'Interface',
  projeto: 'Projeto'
};

export const STATE_TONE: Record<string, Tone> = {
  completed: 'success',
  completed_with_limitations: 'warning',
  failed: 'danger',
  requires_intervention: 'warning',
  awaiting_approval: 'info'
};
