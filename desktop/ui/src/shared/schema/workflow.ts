import { z } from 'zod';

import { runStartedSchema } from './run';

/** Corpo de `input` em workflow.start (engineering.request.v1). Stacks vêm do contexto do motor. */
export const workflowStartInputSchema = z.object({
  goal: z.string().min(1),
  impact: z.boolean().optional(),
  requiresApproval: z.boolean().optional(),
  /** Pede ao motor que o Planning e o Architecture usem o modelo. O motor recusa se não houver modelo configurado. */
  useModel: z.boolean().optional()
});

/** Parâmetros de workflow.start — espelha contracts/schemas/ai/workflow.start.params.schema.json */
export const workflowStartParamsSchema = z
  .object({
    projectId: z.string().min(1),
    input: workflowStartInputSchema.optional()
  })
  .strict();

export const workflowStartResultSchema = runStartedSchema;

export type WorkflowStartInput = z.infer<typeof workflowStartInputSchema>;
