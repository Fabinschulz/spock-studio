import type { Tone } from '@/shared/components/ui';

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
