import { z } from 'zod';

/**
 * Contrato da central de execuções (`run.list` e `run.detail`). Espelha `RunCatalog` no motor e é conferido contra as
 * fixtures de `contracts/fixtures/runs`. Lê só diários: nada aqui dispara, reverte ou valida.
 */

/** Os tipos de execução que o motor reconhece, na ordem em que a tela os oferece. */
export const RUN_KINDS = ['apply', 'validate', 'revert', 'workflow'] as const;

/** Os estados de uma execução, os mesmos de `RunStates` no motor. */
export const RUN_STATES = [
  'awaiting_approval',
  'running',
  'validating',
  'completed',
  'completed_with_limitations',
  'failed',
  'requires_intervention',
  'cancelled'
] as const;

export type RunKind = (typeof RUN_KINDS)[number];
export type RunState = (typeof RUN_STATES)[number];

export const runItemSchema = z.object({
  runId: z.string(),
  projectId: z.string(),
  projectName: z.string(),
  kind: z.enum(RUN_KINDS),
  state: z.enum(RUN_STATES),
  startedAt: z.string(),
  finishedAt: z.string().nullable(),
  /** Andando neste processo agora. Um diário "running" sem isso foi interrompido por uma queda. */
  live: z.boolean(),
  changesApplied: z.boolean(),
  reverted: z.boolean(),
  canRevert: z.boolean(),
  revertsRunId: z.string().nullable(),
  error: z.string().nullable(),
  operations: z.number(),
  pending: z.number(),
  notApplied: z.number(),
  tokens: z.number().nullable()
});

export const runListSchema = z.object({
  runs: z.array(runItemSchema),
  nextCursor: z.string().nullable(),
  summary: z.object({ total: z.number(), running: z.number(), awaitingApproval: z.number() })
});

export const runDetailSchema = runItemSchema.extend({
  operations: z.array(
    z.object({
      path: z.string(),
      op: z.string(),
      source: z.string(),
      status: z.string(),
      note: z.string().nullable()
    })
  ),
  steps: z.array(z.object({ id: z.string(), title: z.string(), status: z.string(), detail: z.string().nullable() })),
  notApplied: z.array(z.string()),
  pending: z.array(z.string()),
  evals: z.array(z.object({ id: z.string(), status: z.string(), required: z.boolean() })),
  actions: z.array(
    z.object({ id: z.string(), kind: z.string(), summary: z.string(), target: z.string(), status: z.string() })
  ),
  model: z.object({ calls: z.number(), inputTokens: z.number(), outputTokens: z.number() }).nullable(),
  eventCount: z.number(),
  events: z.array(z.object({ seq: z.number(), type: z.string() }))
});

export type RunItem = z.infer<typeof runItemSchema>;
export type RunList = z.infer<typeof runListSchema>;
export type RunDetail = z.infer<typeof runDetailSchema>;
