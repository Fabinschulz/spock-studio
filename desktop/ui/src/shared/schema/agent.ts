import { z } from 'zod';

import { snapshotOf } from './run';

/** O que um agente respondeu, já sem segredos. `error` diz por que falhou: não instalado, tempo esgotado ou erro da ferramenta. */
export const agentRunResultSchema = z.object({
  kind: z.literal('agent'),
  tool: z.string(),
  toolName: z.string(),
  exitCode: z.number().nullable(),
  error: z.string().nullable(),
  answer: z.string()
});

export const agentSnapshotSchema = snapshotOf(agentRunResultSchema);

export const agentOutputSchema = z.object({ line: z.string() });

export type AgentRunResult = z.infer<typeof agentRunResultSchema>;
