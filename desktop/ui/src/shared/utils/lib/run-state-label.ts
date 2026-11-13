import type { z } from 'zod';

import type { runOutcomeSchema } from '@/shared/schema';

type RunOutcome = z.infer<typeof runOutcomeSchema>;

export const runOutcomeLabel: Record<RunOutcome, string> = {
  completed: 'Concluído',
  completed_with_limitations: 'Concluído com limitações',
  failed: 'Falhou',
  requires_intervention: 'Requer intervenção',
  cancelled: 'Cancelado'
};

const progressLabel: Record<string, string> = {
  awaiting_approval: 'Aguardando aprovação',
  running: 'Em execução',
  validating: 'Validando'
};

export function runStateLabel(state: string): string {
  return (runOutcomeLabel as Record<string, string>)[state] ?? progressLabel[state] ?? state;
}
