import { z } from 'zod';

import type { WorkflowStartInput } from '@/shared/schema';

export const engineeringRequestFormSchema = z.object({
  goal: z.string().trim().min(1, 'Descreva o objetivo do pedido.'),
  impact: z.boolean(),
  requiresApproval: z.boolean()
});

export type EngineeringRequestFormValues = z.infer<typeof engineeringRequestFormSchema>;

export const engineeringRequestFormDefaults: EngineeringRequestFormValues = {
  goal: '',
  impact: true,
  requiresApproval: false
};

export function toWorkflowStartInput(values: EngineeringRequestFormValues): WorkflowStartInput {
  const goal = values.goal.trim();
  return {
    goal,
    ...(values.impact ? { impact: true } : {}),
    ...(values.requiresApproval ? { requiresApproval: true } : {})
  };
}
