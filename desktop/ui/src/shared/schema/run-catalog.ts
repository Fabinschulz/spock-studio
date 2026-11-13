import { z } from 'zod';

/** Espelha `RunCatalog` no motor e é conferido contra as fixtures de `contracts/fixtures/runs`. */

/** Na ordem em que a tela os oferece. */
export const RUN_KINDS = ['apply', 'validate', 'revert', 'workflow', 'assistant', 'investigation'] as const;

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
  // Texto, não enum: um tipo ou estado novo do motor aparece como veio e não derruba a lista inteira.
  kind: z.string(),
  state: z.string(),
  startedAt: z.string(),
  finishedAt: z.string().nullable(),
  /** Andando neste processo agora. Um diário "running" sem isso foi interrompido por uma queda. */
  live: z.boolean(),
  interrupted: z.boolean(),
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

/** Contagens do diário, sem interpretação: a interface as transforma em frases. */
export const deliverySummarySchema = z.object({
  done: z.object({
    created: z.number(),
    updated: z.number(),
    removed: z.number(),
    reverted: z.number(),
    stepsCompleted: z.number(),
    stepsTotal: z.number(),
    actionsExecuted: z.number(),
    actionsTotal: z.number()
  }),
  /** Nulo quando a execução não registrou nenhuma verificação. */
  validated: z
    .object({
      total: z.number(),
      passed: z.number(),
      failed: z.number(),
      blocked: z.number(),
      notRun: z.number(),
      requiredNotPassed: z.number()
    })
    .nullable(),
  leftOut: z.object({
    notApplied: z.number(),
    pending: z.number(),
    conflicts: z.number(),
    actionsNotExecuted: z.number()
  })
});

/** Título e situação vêm dos dados sincronizados, nunca da ligação. */
export const runLinkSchema = z.object({
  integrationId: z.string(),
  /** Nulo quando a integração foi desvinculada do projeto. */
  provider: z.string().nullable(),
  kind: z.string(),
  externalId: z.string(),
  linkedAt: z.string(),
  /** Falso quando o item saiu do recorte da integração: a ligação fica, o conteúdo some. */
  available: z.boolean(),
  title: z.string().nullable(),
  status: z.string().nullable(),
  statusCategory: z.string().nullable()
});

export const runLinksResultSchema = z.object({ links: z.array(runLinkSchema) });

export const linkedItemSchema = z.object({
  integrationId: z.string(),
  provider: z.string().nullable(),
  kind: z.string(),
  externalId: z.string(),
  available: z.boolean(),
  title: z.string().nullable(),
  status: z.string().nullable(),
  statusCategory: z.string().nullable(),
  runs: z.array(z.object({ runId: z.string(), kind: z.string(), state: z.string(), startedAt: z.string() }))
});

export const linkedItemsResultSchema = z.object({ items: z.array(linkedItemSchema) });

export const runDetailSchema = runItemSchema.extend({
  links: z.array(runLinkSchema),
  summary: deliverySummarySchema,
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

export type LinkedItemsResult = z.infer<typeof linkedItemsResultSchema>;
export type LinkedItem = z.infer<typeof linkedItemSchema>;
export type RunLink = z.infer<typeof runLinkSchema>;
export type RunItem = z.infer<typeof runItemSchema>;
export type RunList = z.infer<typeof runListSchema>;
export type RunDetail = z.infer<typeof runDetailSchema>;
