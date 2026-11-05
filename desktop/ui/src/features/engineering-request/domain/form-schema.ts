import { z } from 'zod';

import type { WorkflowStartInput } from '@/shared/schema';

export const engineeringRequestFormSchema = z.object({
  goal: z.string().trim().min(1, 'Descreva o objetivo do pedido.'),
  impact: z.boolean(),
  requiresApproval: z.boolean(),
  useModel: z.boolean()
});

export type EngineeringRequestFormValues = z.infer<typeof engineeringRequestFormSchema>;

export const engineeringRequestFormDefaults: EngineeringRequestFormValues = {
  goal: '',
  impact: true,
  requiresApproval: false,
  // Sempre desligado de partida: trechos do projeto só saem para o provedor se a pessoa marcar.
  useModel: false
};

export function toWorkflowStartInput(values: EngineeringRequestFormValues): WorkflowStartInput {
  const goal = values.goal.trim();
  return {
    goal,
    ...(values.impact ? { impact: true } : {}),
    ...(values.requiresApproval ? { requiresApproval: true } : {}),
    ...(values.useModel ? { useModel: true } : {})
  };
}
